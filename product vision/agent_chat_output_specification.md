# Agent Chat Output Specification

## 1. Purpose

This specification defines how **agent chat output** is produced, transported, rendered, and constrained within the system.

The goal is to provide **clear, user-facing narration and explanations** without reintroducing:
- conversational memory
- hidden state
- summarization-driven context loss

Chat output is explicitly **non-authoritative** and **disposable**.

---

## 2. Core Principle (Non‑Negotiable)

> **Chat output is a UI projection, not system state.**

Deleting all chat output at any time must not affect correctness, progress, or recovery.

---

## 3. What Chat Output Is (and Is Not)

### 3.1 Chat Output IS

- Human-readable narration of what the system is doing
- Explanations of completed or upcoming actions
- Requests for explicit user input or approval
- Progress updates
- Error explanations

### 3.2 Chat Output IS NOT

- Memory
- Control flow
- Task definition
- State mutation
- Decision authority

Any design that treats chat output as one of the above is invalid.

---

## 4. Authority Separation

The system has two distinct output channels:

### 4.1 Machine Output (Authoritative)

- Structured
- Schema-validated
- Consumed by the Controller
- Drives execution

Examples:
- action proposals
- tool parameters
- artifact metadata

### 4.2 Chat Output (Non‑Authoritative)

- Unstructured natural language
- Rendered only by the UI
- Never parsed for logic

The two channels must **never be merged**.

---

## 5. Sources of Chat Output

Chat output may originate from:

### 5.1 Controller (Preferred)

Controller-generated chat messages are:
- Deterministic
- Trustworthy
- Recommended for:
  - status changes
  - task transitions
  - pause/resume notifications
  - error reporting

### 5.2 Agent (Optional)

Agent-generated chat messages are:
- Explanatory only
- Used sparingly
- Never relied upon

The agent may explain *why* it proposed an action, but never *what happened*.

---

## 6. Chat Message Data Model

A minimal, implementation-agnostic model:

- timestamp
- role (system | agent | user)
- content

Chat messages are **append-only UI events**.

---

## 7. Lifecycle and Retention

### 7.1 Lifetime

- Chat output exists only for the duration of the UI session
- Persistence is optional and read-only

### 7.2 Retention Limits

- UI may cap chat output to N messages
- Old messages may be dropped without warning

Dropping messages must never require summarization.

---

## 8. Prompt Interaction Rules

### 8.1 Prohibited

The following are forbidden:
- Injecting prior chat output into prompts automatically
- Summarizing chat history for context preservation
- Using chat output as implicit memory

### 8.2 Allowed

- Injecting **explicit artifacts** derived from user-approved data
- Injecting **current command only**

---

## 9. Error and Approval Messaging

Chat output is the correct channel for:

- Explaining why an action was rejected
- Asking the user for approval (e.g., script execution)
- Explaining pauses or cancellations

The user's response is treated as a **new command**, not a continuation of chat.

---

## 10. Restart and Recovery Guarantees

On application restart:

- Chat output may be empty
- No attempt is made to reconstruct chat history
- Campaign state is rehydrated exclusively from authoritative storage

The agent must continue correctly without re-explaining past steps unless necessary.

---

## 11. Explicit Anti‑Patterns

The following patterns are invalid:

- "As we discussed earlier…"
- "Based on previous messages…"
- "You asked me before to…"
- Automatic chat summarization
- Treating chat as conversation memory

These indicate leaked authority and must be rejected in code review.

---

## 12. Invariant (Specification Test)

> **If all chat output is deleted at any moment, the system must behave identically.**

Any implementation that fails this invariant violates the specification.

---

## 13. Summary

Chat output exists solely to **communicate with the human**, not to coordinate the system.

It is:
- Ephemeral
- Disposable
- Non-authoritative

The Controller owns truth.
The Database owns memory.
The Agent proposes.
The Chat narrates.

