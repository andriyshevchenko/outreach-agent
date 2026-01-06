# Data Model: Deterministic Desktop Outreach Agent

**Date**: 2026-01-06  
**Purpose**: Define database schema and entity relationships for 8 core entities

---

## Entity Relationship Diagram

```
┌─────────────────┐
│   Configuration │ (user-scoped, encrypted)
│─────────────────│
│ PK: id          │
│    user_id      │◄──┐
│    key_name     │   │
│    encrypted_   │   │
│      value      │   │
└─────────────────┘   │
                      │
┌─────────────────┐   │
│    Campaign     │───┘ (one user, many campaigns)
│─────────────────│
│ PK: id          │
│    name         │
│    status       │◄────────────────────┐
│    created_at   │                     │
└────────┬────────┘                     │
         │                              │
         │ 1:N                          │
         │                              │
         ├────────────────┬─────────────┼──────────┬──────────────┐
         │                │             │          │              │
         ▼                ▼             ▼          ▼              ▼
┌──────────────┐  ┌──────────────┐  ┌──────────┐  ┌────────┐  ┌──────────────┐
│     Task     │  │     Lead     │  │ Artifact │  │AuditLog│  │  EventLog    │
│──────────────│  │──────────────│  │──────────│  │────────│  │──────────────│
│ PK: id       │  │ PK: id       │  │ PK: id   │  │PK: id  │  │ PK: id       │
│ FK: campaign │  │ FK: campaign │  │FK:campgn │  │FK:cmpgn│  │ FK: campaign │
│     _id      │  │     _id      │  │   _id    │  │  _id   │  │     _id      │
│ description  │  │ full_name    │  │ artifact │  │timestmp│  │ timestamp    │
│ status       │  │ profile_url  │  │   _type  │  │action_ │  │ entity_type  │
│ preconditions│  │ job_title    │  │ artifact │  │  type  │  │ entity_id    │
│ metadata     │  │ company      │  │   _key   │  │payload │  │ change_      │
└──────────────┘  │ weight_score │  │ source   │  └────────┘  │   payload    │
                  │ status       │  │ content  │              └──────────────┘
                  └──────────────┘  │created_at│
                                    └──────────┘

┌──────────────────┐
│ ExecutionState   │ (singleton per campaign, current position)
│──────────────────│
│ FK: campaign_id  │◄────────────┐
│     (unique)     │             │
│ current_task_id  │             │
│ last_action_     │             │
│   timestamp      │             │
└──────────────────┘             │
         ▲                       │
         └───────────────────────┘
```

---

## 1. Campaign

**Purpose**: Primary namespace for all campaign-scoped state. Enables campaign list UI (FR-045) and resume workflow (FR-046).

### Schema

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique campaign identifier |
| `name` | TEXT | NOT NULL | User-provided campaign name |
| `status` | TEXT | NOT NULL, CHECK | One of: initializing \| active \| paused \| completed \| error (FR-054) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Campaign creation timestamp |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Last state mutation timestamp |

### Indexes
```sql
CREATE INDEX idx_campaign_status ON campaign(status);
CREATE INDEX idx_campaign_created_at ON campaign(created_at DESC);
```

### State Transitions (FR-052 through FR-057)
```sql
-- Enforced via application logic in CampaignController.cs, not DB constraints
initializing → active (requires environment validation)
active ↔ paused (user action only)
active → completed (all tasks done)
any → error (invariant violation)
```

### Validation Rules
- Status MUST be one of enum values (FR-054)
- State transitions MUST be Controller-only (FR-060, no direct UI updates)

---

## 2. Task

**Purpose**: Work unit within a campaign. Maps to todo list items that persist across sessions (FR-011).

### Schema

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique task identifier |
| `campaign_id` | UUID | FOREIGN KEY (campaign.id), NOT NULL | Parent campaign |
| `description` | TEXT | NOT NULL | Natural language task description |
| `status` | TEXT | NOT NULL, CHECK | One of: pending \| in-progress \| done \| blocked |
| `preconditions` | JSONB | NULLABLE | Array of task IDs that must complete first |
| `metadata` | JSONB | NULLABLE | Extensible data (e.g., retries, error context) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Task creation timestamp |
| `completed_at` | TIMESTAMPTZ | NULLABLE | Task completion timestamp (null if not done) |

### Indexes
```sql
CREATE INDEX idx_task_campaign_id ON task(campaign_id);
CREATE INDEX idx_task_status ON task(campaign_id, status);
CREATE INDEX idx_task_created_at ON task(campaign_id, created_at);
```

### Validation Rules
- Task status cannot be `done` unless side effects verified (FR-007)
- Task cannot be in-progress if preconditions not met (enforced in Controller)

---

## 3. Lead

**Purpose**: LinkedIn prospect data with prioritization score. Supports lead scoring algorithm (FR-029 through FR-035).

### Schema

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique lead identifier |
| `campaign_id` | UUID | FOREIGN KEY (campaign.id), NOT NULL | Parent campaign |
| `full_name` | TEXT | NOT NULL | Lead's full name from LinkedIn |
| `profile_url` | TEXT | NOT NULL, UNIQUE | LinkedIn profile URL |
| `job_title` | TEXT | NULLABLE | Current job title |
| `company` | TEXT | NULLABLE | Current company |
| `weight_score` | REAL | NOT NULL, CHECK (weight_score >= 0 AND weight_score <= 100) | Prioritization score 0-100 (FR-029) |
| `status` | TEXT | NOT NULL, CHECK | One of: pending \| contacted \| responded \| rejected |
| `metadata` | JSONB | NULLABLE | Extensible data (industry, recent_activity, profile_completeness) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Lead creation timestamp |

### Indexes
```sql
CREATE INDEX idx_lead_campaign_id ON lead(campaign_id);
CREATE INDEX idx_lead_weight_score ON lead(campaign_id, weight_score DESC);
CREATE INDEX idx_lead_status ON lead(campaign_id, status);
CREATE UNIQUE INDEX idx_lead_profile_url ON lead(profile_url);
```

### Validation Rules
- `weight_score` MUST be 0-100 (FR-029)
- Duplicate `profile_url` within campaign rejected
- Scoring deterministic: re-running algorithm with same inputs produces same scores (FR-035)

---

## 4. Artifact

**Purpose**: Persisted knowledge (job postings, scoring algorithms, Python scripts, analysis results). Enables artifact retrieval without conversation context (FR-016 through FR-022).

### Schema

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique artifact identifier |
| `campaign_id` | UUID | FOREIGN KEY (campaign.id), NOT NULL | Parent campaign |
| `artifact_type` | TEXT | NOT NULL, CHECK | One of: job_posting \| scoring_algorithm \| python_script \| analysis_result |
| `artifact_key` | TEXT | NOT NULL | Stable identifier (e.g., "algorithm_v1", "job_posting_2026-01-06") |
| `source` | TEXT | NOT NULL, CHECK | One of: user \| agent (FR-022: audit attribution) |
| `content` | JSONB | NOT NULL | Structured or unstructured data |
| `schema_metadata` | JSONB | NULLABLE | Inferred schema for content (FR-017) |
| `version` | INTEGER | NOT NULL, DEFAULT 1 | Artifact version (FR-019: versioning on updates) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Artifact creation timestamp |

### Indexes
```sql
CREATE INDEX idx_artifact_campaign_id ON artifact(campaign_id);
CREATE INDEX idx_artifact_type_key ON artifact(campaign_id, artifact_type, artifact_key);
CREATE INDEX idx_artifact_created_at ON artifact(campaign_id, created_at DESC);
```

### Validation Rules
- `artifact_type` MUST be one of controlled vocabulary (FR-021)
- Artifacts addressable by (campaign_id, artifact_type, artifact_key) tuple (FR-021)
- Artifacts user-scoped: no cross-user access (FR-020)

### Artifact Type Examples
```json
// artifact_type: scoring_algorithm
{
  "criteria": {
    "job_title_relevance": { "weight": 0.40, "target_keywords": ["Senior", "Engineer"] },
    "company_size": { "weight": 0.25, "preferred_range": [50, 500] },
    "recent_activity": { "weight": 0.20, "days_threshold": 30 },
    "profile_completeness": { "weight": 0.15, "min_fields": 5 }
  }
}

// artifact_type: python_script (immutable after approval, FR-043)
{
  "script": "import pandas as pd\ndf = pd.DataFrame(context['leads'])\nresult = df.describe()",
  "approved_by": "user",
  "approved_at": "2026-01-06T10:00:00Z"
}
```

---

## 5. AuditLog

**Purpose**: Tool invocation audit trail. Every tool execution logged (FR-006: no side effect without log entry).

### Schema

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique log entry identifier |
| `campaign_id` | UUID | FOREIGN KEY (campaign.id), NOT NULL | Parent campaign |
| `timestamp` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Action execution timestamp |
| `action_type` | TEXT | NOT NULL | Tool name (e.g., browser_navigate, send_message, python_execute) |
| `payload` | JSONB | NOT NULL | Input parameters and output result |
| `duration_ms` | INTEGER | NULLABLE | Execution duration in milliseconds |
| `success` | BOOLEAN | NOT NULL | Whether action succeeded |

### Indexes
```sql
CREATE INDEX idx_auditlog_campaign_id ON auditlog(campaign_id);
CREATE INDEX idx_auditlog_timestamp ON auditlog(campaign_id, timestamp DESC);
CREATE INDEX idx_auditlog_action_type ON auditlog(campaign_id, action_type);
```

### Validation Rules
- Every Controller tool execution MUST produce audit log entry (FR-006)
- No log entry, no side effect allowed

---

## 6. EventLog

**Purpose**: State mutation audit trail (append-only). Records every entity change for replay (FR-002: persist all state).

### Schema

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique event identifier |
| `campaign_id` | UUID | FOREIGN KEY (campaign.id), NOT NULL | Parent campaign |
| `timestamp` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Event occurrence timestamp |
| `entity_type` | TEXT | NOT NULL | Entity name (campaign, task, lead, etc.) |
| `entity_id` | UUID | NOT NULL | ID of mutated entity |
| `change_payload` | JSONB | NOT NULL | Before/after state or mutation description |

### Indexes
```sql
CREATE INDEX idx_eventlog_campaign_id ON eventlog(campaign_id);
CREATE INDEX idx_eventlog_timestamp ON eventlog(campaign_id, timestamp DESC);
CREATE INDEX idx_eventlog_entity ON eventlog(campaign_id, entity_type, entity_id);
```

### Validation Rules
- Append-only: no updates or deletes allowed
- Every task status change MUST produce event log entry (FR-007)

### Event Payload Example
```json
{
  "event": "task_status_changed",
  "before": { "status": "in-progress" },
  "after": { "status": "done" },
  "verified": true,
  "completed_at": "2026-01-06T10:05:00Z"
}
```

---

## 7. Configuration

**Purpose**: Encrypted environment configuration (API keys, database credentials, LLM settings). Enables multi-device usage (FR-023 through FR-028).

### Schema

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `id` | UUID | PRIMARY KEY | Unique configuration entry identifier |
| `user_id` | UUID | NOT NULL | User identifier (future multi-user support) |
| `key_name` | TEXT | NOT NULL | Configuration key (e.g., openai_api_key, supabase_url) |
| `encrypted_value` | TEXT | NOT NULL | AES-256 encrypted value |
| `config_type` | TEXT | NOT NULL, CHECK | One of: api_key \| llm_provider \| database |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Entry creation timestamp |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Last update timestamp |

### Indexes
```sql
CREATE UNIQUE INDEX idx_configuration_user_key ON configuration(user_id, key_name);
CREATE INDEX idx_configuration_config_type ON configuration(user_id, config_type);
```

### Validation Rules
- No sensitive data stored unencrypted (FR-023, FR-024)
- Agent references secrets by symbolic name only, never sees raw values (FR-028)

### Encryption Details
- Algorithm: AES-256-GCM
- Key derivation: User-specific master key (stored in OS keychain on desktop)
- Rotation: Master key rotatable via UI (re-encrypts all values)

---

## 8. ExecutionState

**Purpose**: Current execution position. Enables resume from exact point (FR-015, SC-001).

### Schema

| Column | Type | Constraints | Description |
|--------|------|-------------|-------------|
| `campaign_id` | UUID | PRIMARY KEY, FOREIGN KEY (campaign.id) | Parent campaign (one-to-one) |
| `current_task_id` | UUID | FOREIGN KEY (task.id), NULLABLE | Task in-progress (null if between cycles) |
| `last_action_timestamp` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Last action completion time |
| `cycle_count` | INTEGER | NOT NULL, DEFAULT 0 | Number of execution cycles completed |

### Indexes
```sql
-- No additional indexes needed (PRIMARY KEY on campaign_id sufficient)
```

### Validation Rules
- One ExecutionState per campaign (enforced via PRIMARY KEY on campaign_id)
- Updated every execution cycle after Step 7 (FR-005g: Update State)

---

## Schema Validation Rules (Cross-Entity)

### Referential Integrity
- All foreign keys MUST use `ON DELETE CASCADE` (deleting campaign deletes all child entities)
- ExecutionState.current_task_id MUST reference existing task or be NULL

### Deterministic Replay (Constitution Principle II)
- EventLog enables full state reconstruction from event history
- No implicit state: all data required for resume stored explicitly

### Performance Targets
- Campaign list query: <500ms for 100 campaigns (FR-045 UI responsiveness)
- State load per cycle: <500ms for campaign + tasks + leads (FR-009 fresh state reload)
- Artifact retrieval: <2s (constraint from spec)

---

## Migration Strategy

**Phase 1**: Create schema via Supabase migrations
```sql
-- migrations/001_create_schema.sql
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE campaign (...);
CREATE TABLE task (...);
CREATE TABLE lead (...);
CREATE TABLE artifact (...);
CREATE TABLE auditlog (...);
CREATE TABLE eventlog (...);
CREATE TABLE configuration (...);
CREATE TABLE executionstate (...);

-- Add indexes and constraints
```

**Phase 2**: Seed test data (10 campaigns, 50 leads) for integration tests

**Phase 3**: Enable Row-Level Security (RLS) for multi-user support (future)
```sql
-- Future: Multi-user RLS policies
ALTER TABLE campaign ENABLE ROW LEVEL SECURITY;
CREATE POLICY campaign_isolation ON campaign
  USING (user_id = current_user_id());
```

---

## Summary

**Entities**: 8 (Campaign, Task, Lead, Artifact, AuditLog, EventLog, Configuration, ExecutionState)  
**Relationships**: 1:N (Campaign → all children), 1:1 (Campaign ↔ ExecutionState)  
**Primary Keys**: All UUIDs (enables distributed ID generation)  
**Foreign Keys**: All with ON DELETE CASCADE for referential integrity  
**Indexes**: 20 total (optimized for query patterns: campaign_id, status, timestamps)

**Alignment with Constitution**:
- ✅ Principle I (Controller Authority): Schema supports Controller-only state mutations
- ✅ Principle II (Deterministic State): Full state reconstruction from database alone (EventLog replay)
- ✅ Principle VI (Observability): AuditLog + EventLog provide complete audit trail
