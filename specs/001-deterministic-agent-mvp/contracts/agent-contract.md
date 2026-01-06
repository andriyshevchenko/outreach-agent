# Agent Contract: Controller ↔ Agent Interface

**Purpose**: Define JSON schema contracts for 8 finite action types (FR-005i through FR-005p) and state snapshot format (FR-050).

---

## Request: State Snapshot (Controller → Agent)

**Description**: Controller provides agent with complete state snapshot for current execution cycle. Agent MUST NOT assume memory of prior cycles (Constitution Principle II).

### Schema

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "StateSnapshot",
  "type": "object",
  "required": ["campaign", "current_task", "pending_tasks", "leads_summary", "recent_audit_log"],
  "properties": {
    "campaign": {
      "type": "object",
      "required": ["id", "name", "status"],
      "properties": {
        "id": { "type": "string", "format": "uuid" },
        "name": { "type": "string" },
        "status": { 
          "type": "string", 
          "enum": ["initializing", "active", "paused", "completed", "error"] 
        }
      }
    },
    "current_task": {
      "oneOf": [
        { "type": "null" },
        {
          "type": "object",
          "required": ["id", "description", "status"],
          "properties": {
            "id": { "type": "string", "format": "uuid" },
            "description": { "type": "string" },
            "status": { 
              "type": "string", 
              "enum": ["pending", "in-progress", "done", "blocked"] 
            },
            "preconditions": {
              "type": "array",
              "items": { "type": "string", "format": "uuid" }
            }
          }
        }
      ]
    },
    "pending_tasks": {
      "type": "array",
      "maxItems": 10,
      "items": {
        "type": "object",
        "required": ["id", "description"],
        "properties": {
          "id": { "type": "string", "format": "uuid" },
          "description": { "type": "string" }
        }
      }
    },
    "leads_summary": {
      "type": "object",
      "required": ["total", "pending", "contacted", "responded", "current_lead"],
      "properties": {
        "total": { "type": "integer", "minimum": 0 },
        "pending": { "type": "integer", "minimum": 0 },
        "contacted": { "type": "integer", "minimum": 0 },
        "responded": { "type": "integer", "minimum": 0 },
        "current_lead": {
          "oneOf": [
            { "type": "null" },
            {
              "type": "object",
              "required": ["id", "full_name", "profile_url"],
              "properties": {
                "id": { "type": "string", "format": "uuid" },
                "full_name": { "type": "string" },
                "profile_url": { "type": "string", "format": "uri" },
                "job_title": { "type": "string" },
                "company": { "type": "string" },
                "weight_score": { "type": "number", "minimum": 0, "maximum": 100 }
              }
            }
          ]
        }
      }
    },
    "recent_audit_log": {
      "type": "array",
      "maxItems": 5,
      "items": {
        "type": "object",
        "required": ["action_type", "timestamp", "success"],
        "properties": {
          "action_type": { "type": "string" },
          "timestamp": { "type": "string", "format": "date-time" },
          "success": { "type": "boolean" },
          "payload": { "type": "object" }
        }
      }
    },
    "available_artifacts": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["artifact_type", "artifact_key"],
        "properties": {
          "artifact_type": { "type": "string" },
          "artifact_key": { "type": "string" }
        }
      }
    }
  }
}
```

### Example
```json
{
  "campaign": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Q1 2026 Outreach",
    "status": "active"
  },
  "current_task": {
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "description": "Send connection request to lead #3",
    "status": "in-progress",
    "preconditions": []
  },
  "pending_tasks": [
    {
      "id": "770e8400-e29b-41d4-a716-446655440002",
      "description": "Follow up on connection request"
    }
  ],
  "leads_summary": {
    "total": 100,
    "pending": 97,
    "contacted": 3,
    "responded": 0,
    "current_lead": {
      "id": "880e8400-e29b-41d4-a716-446655440003",
      "full_name": "Jane Doe",
      "profile_url": "https://linkedin.com/in/janedoe",
      "job_title": "Senior Engineer",
      "company": "TechCorp",
      "weight_score": 87.5
    }
  },
  "recent_audit_log": [
    {
      "action_type": "browser_navigate",
      "timestamp": "2026-01-06T10:00:00Z",
      "success": true,
      "payload": { "url": "https://linkedin.com/in/johndoe" }
    }
  ],
  "available_artifacts": [
    {
      "artifact_type": "scoring_algorithm",
      "artifact_key": "algorithm_v1"
    }
  ]
}
```

---

## Response: Agent Proposal (Agent → Controller)

**Description**: Agent proposes ONE action from finite vocabulary (FR-005i through FR-005p). Invalid proposals rejected (FR-010).

### Proposal Types

#### 1. create_task (FR-005i)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "CreateTaskProposal",
  "type": "object",
  "required": ["action_type", "task"],
  "properties": {
    "action_type": { "const": "create_task" },
    "task": {
      "type": "object",
      "required": ["description"],
      "properties": {
        "description": { 
          "type": "string",
          "minLength": 10,
          "maxLength": 500
        },
        "preconditions": {
          "type": "array",
          "items": { "type": "string", "format": "uuid" }
        }
      }
    },
    "justification": { "type": "string" }
  }
}
```

**Example**:
```json
{
  "action_type": "create_task",
  "task": {
    "description": "Wait 24 hours for connection request acceptance",
    "preconditions": ["660e8400-e29b-41d4-a716-446655440001"]
  },
  "justification": "LinkedIn best practices recommend 24-hour wait before follow-up"
}
```

---

#### 2. select_next_task (FR-005j)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "SelectNextTaskProposal",
  "type": "object",
  "required": ["action_type", "task_id"],
  "properties": {
    "action_type": { "const": "select_next_task" },
    "task_id": { "type": "string", "format": "uuid" },
    "reasoning": { "type": "string" }
  }
}
```

**Example**:
```json
{
  "action_type": "select_next_task",
  "task_id": "770e8400-e29b-41d4-a716-446655440002",
  "reasoning": "All preconditions met, next highest priority task"
}
```

---

#### 3. execute_tool (FR-005k)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "ExecuteToolProposal",
  "type": "object",
  "required": ["action_type", "tool_name", "parameters"],
  "properties": {
    "action_type": { "const": "execute_tool" },
    "tool_name": { 
      "type": "string",
      "enum": ["browser_navigate", "browser_click", "browser_type", "send_message", "python_execute"]
    },
    "parameters": { "type": "object" },
    "expected_outcome": { "type": "string" }
  }
}
```

**Example**:
```json
{
  "action_type": "execute_tool",
  "tool_name": "browser_navigate",
  "parameters": {
    "url": "https://linkedin.com/in/janedoe"
  },
  "expected_outcome": "Navigate to Jane Doe's LinkedIn profile"
}
```

---

#### 4. generate_message (FR-005l)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "GenerateMessageProposal",
  "type": "object",
  "required": ["action_type", "message"],
  "properties": {
    "action_type": { "const": "generate_message" },
    "message": {
      "type": "object",
      "required": ["type", "content"],
      "properties": {
        "type": { 
          "type": "string",
          "enum": ["connection_request", "follow_up", "direct_message"] 
        },
        "content": { 
          "type": "string",
          "minLength": 20,
          "maxLength": 300
        },
        "personalization_context": { "type": "object" }
      }
    },
    "requires_approval": { "type": "boolean", "default": true }
  }
}
```

**Example**:
```json
{
  "action_type": "generate_message",
  "message": {
    "type": "connection_request",
    "content": "Hi Jane, I noticed your work at TechCorp on cloud infrastructure. Would love to connect and share insights.",
    "personalization_context": {
      "job_title": "Senior Engineer",
      "company": "TechCorp"
    }
  },
  "requires_approval": true
}
```

---

#### 5. analyze_leads (FR-005m)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "AnalyzeLeadsProposal",
  "type": "object",
  "required": ["action_type", "analysis_type"],
  "properties": {
    "action_type": { "const": "analyze_leads" },
    "analysis_type": { 
      "type": "string",
      "enum": ["prioritize", "categorize", "score_update"] 
    },
    "parameters": { "type": "object" }
  }
}
```

**Example**:
```json
{
  "action_type": "analyze_leads",
  "analysis_type": "prioritize",
  "parameters": {
    "campaign_goal": "Find engineers at 50-500 person companies",
    "weight_preferences": {
      "job_title_relevance": 0.40,
      "company_size": 0.25,
      "recent_activity": 0.20,
      "profile_completeness": 0.15
    }
  }
}
```

---

#### 6. request_user_input (FR-005n)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "RequestUserInputProposal",
  "type": "object",
  "required": ["action_type", "question"],
  "properties": {
    "action_type": { "const": "request_user_input" },
    "question": { 
      "type": "string",
      "minLength": 10 
    },
    "options": {
      "type": "array",
      "items": { "type": "string" }
    },
    "context": { "type": "string" }
  }
}
```

**Example**:
```json
{
  "action_type": "request_user_input",
  "question": "This lead's profile is incomplete. Should I proceed or skip?",
  "options": ["Proceed", "Skip", "Manual review"],
  "context": "Lead has no job title or company information"
}
```

---

#### 7. persist_artifact (FR-005o)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "PersistArtifactProposal",
  "type": "object",
  "required": ["action_type", "artifact"],
  "properties": {
    "action_type": { "const": "persist_artifact" },
    "artifact": {
      "type": "object",
      "required": ["artifact_type", "artifact_key", "content"],
      "properties": {
        "artifact_type": { 
          "type": "string",
          "enum": ["job_posting", "scoring_algorithm", "python_script", "analysis_result"]
        },
        "artifact_key": { "type": "string" },
        "content": { "type": "object" }
      }
    },
    "requires_approval": { "type": "boolean", "default": true }
  }
}
```

**Example**:
```json
{
  "action_type": "persist_artifact",
  "artifact": {
    "artifact_type": "scoring_algorithm",
    "artifact_key": "algorithm_v2",
    "content": {
      "criteria": {
        "job_title_relevance": { "weight": 0.50, "target_keywords": ["Senior", "Principal"] }
      }
    }
  },
  "requires_approval": true
}
```

---

#### 8. no_op (FR-005p)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "NoOpProposal",
  "type": "object",
  "required": ["action_type", "reason"],
  "properties": {
    "action_type": { "const": "no_op" },
    "reason": { 
      "type": "string",
      "enum": ["awaiting_user_input", "preconditions_not_met", "rate_limit_reached", "campaign_complete"]
    },
    "details": { "type": "string" }
  }
}
```

**Example**:
```json
{
  "action_type": "no_op",
  "reason": "rate_limit_reached",
  "details": "LinkedIn rate limit: wait 60 minutes before next connection request"
}
```

---

## Validation Rules (ProposalValidator.cs)

### Schema Validation (FR-005d)
1. Proposal MUST conform to one of 8 action type schemas
2. Invalid schemas rejected immediately (no partial execution)
3. Unknown action types rejected (FR-010)

### Business Logic Validation
1. `execute_tool` with `tool_name` not in enum → rejected
2. `select_next_task` with non-existent `task_id` → rejected
3. `generate_message` with `content` >300 chars → rejected (LinkedIn limits)
4. `create_task` without `justification` for complex campaigns → warning (optional field)

### Controller Invariant Checks (FR-006 through FR-010)
1. All tool executions produce audit log entry (FR-006)
2. Task status changes require verification (FR-007)
3. All tools mediated by Controller (FR-008)
4. Invalid outputs trigger retry or abort (FR-010)

---

## Contract Testing Strategy

### Unit Tests (OutreachAgent.Core.Tests)
```csharp
[Theory]
[InlineData("create_task", "{\"action_type\":\"create_task\",\"task\":{\"description\":\"Test\"}}")]
[InlineData("no_op", "{\"action_type\":\"no_op\",\"reason\":\"awaiting_user_input\"}")]
public void ProposalValidator_ValidSchema_ReturnsValid(string actionType, string json)
{
    var validator = new ProposalValidator();
    var result = validator.Validate(json);
    Assert.True(result.IsValid);
    Assert.Equal(actionType, result.ActionType);
}

[Fact]
public void ProposalValidator_InvalidActionType_ReturnsInvalid()
{
    var validator = new ProposalValidator();
    var result = validator.Validate("{\"action_type\":\"invalid_action\"}");
    Assert.False(result.IsValid);
    Assert.Contains("unknown action type", result.Error);
}
```

### Integration Tests (OutreachAgent.Integration.Tests)
```csharp
[Fact]
public async Task AgentService_ProposesValidAction_ControllerExecutes()
{
    var state = await _stateManager.LoadStateAsync(campaignId);
    var proposal = await _agentService.InvokeAgentAsync(state);
    
    var validationResult = _proposalValidator.Validate(proposal);
    Assert.True(validationResult.IsValid);
    
    await _controller.ExecuteProposalAsync(proposal);
    
    var auditLogs = await _repository.GetAuditLogsAsync(campaignId);
    Assert.NotEmpty(auditLogs); // FR-006: no side effect without log
}
```

---

## Summary

**Contract Types**: 2 (Request: StateSnapshot, Response: 8 proposal types)  
**Total Schemas**: 10 (1 request + 8 proposals + 1 validation error response)  
**Validation Layers**: 3 (JSON schema, business logic, invariant checks)

**Alignment with Requirements**:
- ✅ FR-005i through FR-005p: All 8 action types defined with strict schemas
- ✅ FR-005d: ProposalValidator enforces schema compliance
- ✅ FR-010: Invalid outputs rejected, no partial execution
- ✅ FR-050: State snapshot format specified with bounded context (max 10 tasks, 5 audit logs)
