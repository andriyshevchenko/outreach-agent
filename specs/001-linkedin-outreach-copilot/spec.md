# Feature Specification: LinkedIn Outreach Copilot Agent

**Feature Branch**: `001-linkedin-outreach-copilot`  
**Created**: January 7, 2026  
**Status**: Draft  
**Input**: User description: "Create a comprehensive feature specification for a LinkedIn Outreach Copilot Agent as a .NET MAUI Blazor Hybrid desktop-first application with deterministic state-driven architecture, campaign management, task verification, and LinkedIn automation via Playwright MCP"

## Clarifications

### Session 2026-01-07

- Q: What rate limiting strategy should be used for LinkedIn automation to avoid account restrictions while maintaining productivity? → A: Adaptive rate limiting with daily quotas - Variable delays (30-90 seconds for connection requests, 5-15 seconds for profile views) plus daily action limits (50 connection requests per day, 100 profile views per day) that reset at midnight user's timezone, with maximum 2-hour active session durations.

- Q: What lead volumes should the system support per campaign and how should the UI handle displaying large numbers of leads? → A: Moderate scale with pagination - Support up to 1,000 leads per campaign with paginated UI display (50 leads per page), database indexes on priority_score and created_at for fast sorting, and lazy loading to keep UI responsive.

- Q: What encryption mechanism and key management strategy should be used for credential storage in the desktop application? → A: Azure Key Vault integration - Store credentials in Azure Key Vault for enterprise-grade security with centralized key management, audit logging, and access policies. Requires internet connectivity for credential retrieval but provides highest security standard.

- Q: How should MCP servers (Playwright, Desktop-Commander, Fetch, Exa) be deployed, managed, and configured with the desktop application? → A: Embedded MCP servers with auto-start - Bundle MCP server executables with the application installer, start them automatically as background processes on application launch, and manage their lifecycle (start/stop/restart) through the Controller. Configuration stored in app settings.

- Q: What application-level monitoring, performance metrics collection, and diagnostic capabilities should be implemented for production troubleshooting? → A: Application Insights integration - Integrate Azure Application Insights for telemetry including performance metrics, error tracking, dependency tracking, and custom events for key operations (campaign actions, LinkedIn automation, task completions) with distributed tracing for MCP tool calls.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Campaign Creation and Configuration (Priority: P1)

As a user, I want to create a new LinkedIn outreach campaign by providing basic information (campaign name, target audience description, context) so that the system can help me conduct structured outreach without losing track of my goals.

**Why this priority**: This is the foundation of the entire system. Without the ability to create and configure campaigns, no other functionality can be used. It establishes the core data model and user entry point.

**Independent Test**: Can be fully tested by creating a new campaign through the UI, verifying it's saved to the database, and confirming the campaign can be selected later without any other functionality present. Delivers immediate value by allowing users to organize their outreach efforts.

**Acceptance Scenarios**:

1. **Given** I am on the main application page, **When** I initiate campaign creation and provide campaign name "SaaS Outreach 2026", target context "DeployBook.app - SaaS for software teams", and specify a working directory, **Then** a new campaign is created with a unique ID, stored in the database, and displayed in my campaign list.

2. **Given** I have created a campaign, **When** I provide LinkedIn credentials and other environment secrets through the settings interface, **Then** these credentials are securely stored (encrypted at rest) and associated with my campaign without being exposed in chat output.

3. **Given** I have an existing campaign, **When** I view the campaign details, **Then** I see the campaign name, context, working directory path, and creation date without seeing any technical implementation details or environment variables.

---

### User Story 2 - Deterministic Execution Cycle with State Recovery (Priority: P1)

As a user, I want the agent to continue my campaign from exactly where it left off even if I close the application, clear chat history, or the conversation context is lost, so that my work is never lost and progress is always maintained.

**Why this priority**: This is the core architectural requirement that distinguishes this agent from problematic implementations. Without deterministic state recovery, all other features become unreliable. This directly addresses the user's pain points with previous GitHub Copilot issues.

**Independent Test**: Can be fully tested by starting a campaign, performing actions that create tasks and artifacts, closing the application, deleting all chat history, reopening the application, and verifying the agent resumes with complete state awareness without referencing prior conversations. Delivers the critical value of reliability and continuity.

**Acceptance Scenarios**:

1. **Given** I have an active campaign with tasks in various states, **When** I close the application and reopen it, **Then** the Controller loads the campaign state from the database and presents the current status without requiring conversation history.

2. **Given** I have completed some campaign tasks, **When** I delete all chat messages and request status update, **Then** the agent provides accurate current state by reading from database without referencing "as we discussed earlier" or prior messages.

3. **Given** the agent has proposed an action in the previous cycle, **When** a new cycle begins, **Then** the Controller reloads complete state from database before requesting the next agent proposal, ensuring no reliance on conversation memory.

4. **Given** a task has dependencies on prior completed work, **When** I resume the campaign after a restart, **Then** the system verifies all prerequisite side effects exist in the database before proceeding with dependent tasks.

---

### User Story 3 - Task Management with Verification (Priority: P1)

As a user, I want all campaign work broken down into concrete tasks with clear completion criteria, and I want each task completion to be verified before moving forward, so that I have confidence the campaign is progressing correctly and nothing is being forgotten.

**Why this priority**: Task management with verification enforces the invariants that prevent the "forgotten todos" and "no database updates" problems. It's the mechanism that ensures completeness and correctness of campaign execution.

**Independent Test**: Can be fully tested by creating a campaign, observing task creation, manually or automatically completing tasks, and verifying that task status changes only occur after side effects are confirmed in the database. Delivers value by providing clear visibility into campaign progress and ensuring reliability.

**Acceptance Scenarios**:

1. **Given** I have created a new campaign, **When** the agent analyzes my campaign context, **Then** a comprehensive task list is generated and stored in the database with each task having a unique ID, description, status (pending/in-progress/completed), and dependencies.

2. **Given** a task requires a side effect (e.g., "scrape LinkedIn profile"), **When** the agent proposes executing that task, **Then** the Controller logs the intent before execution, performs the action, verifies the result, logs the completion, and only then updates task status to completed.

3. **Given** a task is marked as completed, **When** I view the task details, **Then** I can see the audit log entries showing when it was started, what side effects were performed, and when it was verified.

4. **Given** multiple tasks exist with dependencies, **When** the agent proposes the next action, **Then** it only suggests tasks whose prerequisites are marked as completed and verified in the database.

5. **Given** a task execution fails, **When** the error occurs, **Then** the task status remains unchanged (not marked completed), the failure is logged, and the agent proposes a recovery action based on the logged error state.

---

### User Story 4 - LinkedIn Profile Discovery and Lead Collection (Priority: P2)

As a user, I want the system to discover LinkedIn profiles matching my campaign criteria and collect them as leads with relevant profile information, so that I have a pool of potential contacts for outreach.

**Why this priority**: This provides the core value proposition of LinkedIn outreach - finding relevant people. It depends on P1 features (campaign context, task management) but is a critical enabler for actual outreach activities.

**Independent Test**: Can be fully tested by providing campaign criteria, executing lead discovery through LinkedIn search automation, and verifying leads are saved to the database with profile information. Delivers value by automating the manual work of finding relevant contacts.

**Acceptance Scenarios**:

1. **Given** I have a campaign with target audience description "CTOs of SaaS companies with 10-50 employees", **When** the agent executes lead discovery, **Then** it uses Playwright MCP to search LinkedIn, extracts profile information (name, headline, company, location), and stores each lead in the database with campaign association.

2. **Given** leads have been collected, **When** I view the leads list, **Then** I see each lead with key information and a priority score calculated using weighted heuristics based on profile match to campaign goals.

3. **Given** a lead is discovered during LinkedIn automation, **When** the lead data is extracted, **Then** the system stores profile URL, name, headline, company, location, and any notes from the profile that match campaign keywords, without storing unnecessary data.

4. **Given** leads exist from a previous session, **When** the system discovers a profile it has seen before, **Then** it recognizes the duplicate (by LinkedIn URL) and updates existing lead record rather than creating duplicate entries.

---

### User Story 5 - Lead Prioritization (Priority: P2)

As a user, I want leads to be automatically prioritized based on how well they match my campaign goals, so that I focus my outreach efforts on the most promising contacts first.

**Why this priority**: Without prioritization, users waste time on less relevant leads. This depends on lead collection (P2) and provides optimization on top of basic functionality.

**Independent Test**: Can be fully tested by collecting leads with varying profile characteristics, running the prioritization algorithm, and verifying leads are ranked appropriately based on campaign criteria. Delivers value by improving outreach efficiency.

**Acceptance Scenarios**:

1. **Given** I have collected leads with various profiles, **When** the prioritization algorithm runs, **Then** each lead receives a priority score based on weighted heuristics (e.g., headline keyword match: 30%, company size match: 25%, seniority level: 25%, location relevance: 10%, activity level: 10%).

2. **Given** leads have priority scores, **When** I view the leads list, **Then** leads are displayed in descending priority order with the score visible, and I can see the reasoning behind each score (which factors contributed).

3. **Given** my campaign targets "decision makers in DevOps", **When** a lead has "DevOps Director" in their headline, **Then** their priority score is significantly higher than a lead with "DevOps Engineer" due to decision-making authority weighting.

---

### User Story 6 - Outreach Message Generation (Priority: P2)

As a user, I want the system to generate personalized connection requests and messages in Ukrainian with a warm, professional tone based on lead profiles and campaign context, so that my outreach feels authentic and relevant.

**Why this priority**: This automates the most time-consuming part of outreach - crafting personalized messages. Depends on lead collection and prioritization. Critical for campaign execution but not needed for campaign setup.

**Independent Test**: Can be fully tested by selecting a lead, requesting message generation with campaign context, and verifying the generated message is in Ukrainian, uses information from the lead's profile, and maintains warm professional tone. Delivers value by saving hours of manual message writing.

**Acceptance Scenarios**:

1. **Given** I have a high-priority lead "Andriy, CTO at TechStartup focused on cloud infrastructure", **When** I request message generation for my DeployBook.app campaign, **Then** the system generates a connection request in Ukrainian that references their cloud infrastructure focus and explains how DeployBook solves environment management challenges for their team size.

2. **Given** a message is generated, **When** I review it, **Then** it is 200-300 characters for connection request (LinkedIn limit), uses warm tone ("Привіт", not overly formal), avoids sales pitch in initial contact, and includes one specific detail from their profile.

3. **Given** I want to send a follow-up message after connection acceptance, **When** I request follow-up message generation, **Then** the system generates a longer message (up to 1000 characters) with more details about DeployBook, specific use cases matching their profile, and a clear call-to-action.

---

### User Story 7 - LinkedIn Automation with Playwright (Priority: P2)

As a user, I want the system to interact with LinkedIn on my behalf using browser automation, so that I can execute outreach tasks without manual clicking and navigation.

**Why this priority**: Enables scalable campaign execution. Depends on P1 foundation and provides the mechanism for P2 features (lead collection, message sending). Requires careful implementation to avoid LinkedIn restrictions.

**Independent Test**: Can be fully tested by initiating a LinkedIn action (search, view profile, send connection request) and verifying the Playwright MCP tool successfully executes the action in the browser while respecting rate limits and human-like behavior patterns. Delivers value by automating repetitive manual tasks.

**Acceptance Scenarios**:

1. **Given** I have LinkedIn credentials configured, **When** the agent proposes a LinkedIn search action, **Then** the Controller validates the proposal, executes it via Playwright MCP, waits for results to load, and confirms successful execution before marking the task complete.

2. **Given** the agent needs to send a connection request, **When** the Controller executes this via Playwright, **Then** it navigates to the profile, clicks "Connect", enters the personalized message, submits, waits for confirmation, and logs the action with timestamp and result.

3. **Given** multiple LinkedIn actions are queued, **When** the agent proposes rapid execution, **Then** the Controller enforces rate limiting (e.g., minimum 30 seconds between connection requests) to mimic human behavior and avoid account restrictions.

4. **Given** a Playwright action fails (timeout, element not found, network error), **When** the error occurs, **Then** the Controller logs the failure without marking the task complete, and the agent proposes either a retry or alternative approach based on the error type.

---

### User Story 8 - Chat Interface for Agent Interaction (Priority: P1)

As a user, I want to interact with the agent through a chat interface where I can provide input, upload files, and see status updates, so that I have a natural conversational experience while maintaining full control.

**Why this priority**: This is the primary user interface for the application. While the architecture emphasizes that chat is disposable, the chat UI is still how users communicate with the system. Essential for any user interaction.

**Independent Test**: Can be fully tested by launching the application, typing messages, receiving responses, uploading a file, and verifying all interactions work without other features present. Delivers value by providing the communication channel with the agent.

**Acceptance Scenarios**:

1. **Given** I am on the chat page, **When** I type a message like "Create a new campaign for SaaS CTOs" and press enter, **Then** the message appears in the chat, is sent to the Controller, and the agent's response appears within a reasonable time.

2. **Given** I want to provide campaign context via document, **When** I click the file upload button and select a PDF or text file, **Then** the file is uploaded to the agent's working directory, the file path is sent to the Controller, and the agent acknowledges receipt of the file.

3. **Given** the agent needs additional information, **When** it requests user input in chat, **Then** the message clearly states what information is needed and why, without exposing technical details like tool names or implementation specifics.

4. **Given** the Controller is executing a task, **When** the task is in progress, **Then** I see status messages like "Searching LinkedIn for leads..." (generated by Controller) rather than seeing technical details about MCP tools or API calls.

5. **Given** I scroll through chat history, **When** I view older messages, **Then** I can see the conversation flow, but I understand that deleting these messages will not affect campaign state or agent's ability to continue work.

---

### User Story 9 - Settings Configuration (Priority: P1)

As a user, I want a dedicated settings page where I can configure LinkedIn credentials, working directory, and other campaign parameters, so that I can manage configuration separately from chat interactions.

**Why this priority**: Essential for security and usability. Credentials and configuration should not be entered through chat where they might be logged or exposed. This supports the security requirement of never sharing environment variables in chat.

**Independent Test**: Can be fully tested by accessing settings page, entering configuration values, saving them, and verifying they persist across application restarts without other features active. Delivers value by providing secure configuration management.

**Acceptance Scenarios**:

1. **Given** I am on the settings page, **When** I enter LinkedIn credentials (email and password) and save, **Then** the credentials are encrypted and stored securely in campaign configuration, never displayed in plain text, and never appear in chat output.

2. **Given** I need to specify a working directory, **When** I use the directory picker on settings page, **Then** I can browse folders, select a directory, and the path is saved as the agent's working directory for file operations.

3. **Given** I have multiple campaigns, **When** I switch campaigns on settings page, **Then** the correct configuration (credentials, working directory) for the selected campaign is loaded and used for subsequent operations.

4. **Given** I want to update my configuration, **When** I change settings and save, **Then** the new values take effect immediately for the next agent cycle without requiring application restart.

---

### User Story 10 - Audit Logging and Verification (Priority: P1)

As a user, I want every significant action logged with details before execution, so that I can verify what happened, troubleshoot issues, and have confidence that the system is working correctly.

**Why this priority**: Enforces the critical invariant "No side effect without a log entry". Essential for debugging, accountability, and user trust. Part of the core architecture that prevents issues seen in previous implementations.

**Independent Test**: Can be fully tested by executing any action (create campaign, execute task, save artifact), checking the audit log table in database, and verifying the log entry exists with timestamp, action type, and details before side effects occurred. Delivers value by providing transparency and troubleshooting capability.

**Acceptance Scenarios**:

1. **Given** the agent proposes sending a LinkedIn connection request, **When** the Controller validates and approves the action, **Then** a log entry is created with timestamp, campaign_id, action type "connection_request", target profile URL, and status "initiated" BEFORE the Playwright tool is invoked.

2. **Given** an action has been executed, **When** the action completes (success or failure), **Then** the log entry is updated with completion timestamp, final status, and result details (e.g., "connection request sent successfully" or "error: rate limit exceeded").

3. **Given** I am troubleshooting why a task failed, **When** I view the audit logs filtered by task_id, **Then** I see the complete sequence of attempted actions, their results, and timestamps, allowing me to identify exactly where and why the failure occurred.

4. **Given** a campaign has been running for days, **When** I view the audit log summary, **Then** I can see metrics like total actions attempted, success rate, most common errors, and timeline of activity.

---

### Edge Cases

- **Context Truncation**: What happens when conversation history exceeds model context limits? The system MUST continue operating correctly because it relies on database state snapshots, not conversation history.

- **Database Connection Loss**: How does system handle temporary database unavailability? The Controller MUST detect the failure, halt agent cycles, log the error, and retry with exponential backoff until connection is restored.

- **LinkedIn Account Restrictions**: What happens if LinkedIn detects automation and temporarily restricts the account? The system MUST detect login failures or CAPTCHA challenges, pause automation tasks, log the restriction event, and notify the user to resolve manually.

- **Partial Task Completion**: If a task is 90% complete when the application crashes, how does recovery work? The Controller MUST reload state, detect incomplete task (status not "completed"), and either resume or restart the task based on logged side effects.

- **File Upload Limits**: What happens when user uploads a file larger than the working directory quota or MCP file size limit? The system MUST validate file size before upload, reject with clear error message if too large, and suggest alternatives (chunk the file, use external link).

- **Concurrent Campaign Execution**: If user tries to run multiple campaigns simultaneously, how are resources managed? The system MUST allow only one active campaign at a time (Controller enforces this), queueing other campaign requests until current campaign is paused or completed.

- **Malformed Agent Output**: What happens if the agent returns invalid JSON or proposes an action that violates invariants? The Controller MUST validate all agent output against schema, reject invalid proposals, log the validation failure, and request re-proposal with clarification.

- **Duplicate Leads**: How does the system handle discovering the same LinkedIn profile multiple times? The system MUST check lead uniqueness by LinkedIn profile URL before insertion, update existing lead record if found, and log the duplicate detection.

- **Stale Credentials**: What happens if LinkedIn credentials expire or become invalid mid-campaign? The Controller MUST detect authentication failures during Playwright operations, pause the campaign, log the credential error, and prompt user to update credentials on settings page.

- **Empty Campaign Context**: If user creates campaign without providing adequate context, can the agent still operate? The system MUST validate minimum campaign context (at least target audience description), reject campaigns with insufficient context, and prompt user for required information.

## Requirements *(mandatory)*

### Functional Requirements

**Architecture & State Management**

- **FR-001**: System MUST implement a Controller-Agent architecture where the Controller (C# .NET) owns all state, database operations, and tool execution, while the Agent (LLM) is stateless and proposes one action per cycle.

- **FR-002**: Controller MUST reload complete campaign state from database at the start of every execution cycle, including campaign configuration, all tasks, all artifacts, and all audit logs relevant to current context.

- **FR-003**: System MUST use Supabase (PostgreSQL) as the single source of truth for all persistent state, with no reliance on conversation history, in-memory caches, or chat logs for state reconstruction.

- **FR-004**: Agent MUST receive a complete state snapshot in every cycle containing: campaign context, current task list with statuses, current artifacts (leads, messages, etc.), and recent relevant audit logs.

- **FR-005**: Agent MUST propose exactly ONE action per cycle, formatted as structured output (JSON) containing action type, parameters, and reasoning.

- **FR-006**: Controller MUST validate every agent proposal against invariants and business rules before execution, rejecting invalid proposals with specific error explanation.

**Invariants (Non-Negotiable)**

- **FR-007**: System MUST create an audit log entry with timestamp, action type, and parameters BEFORE executing any side effect (database write, tool invocation, file operation).

- **FR-008**: System MUST NOT update task status to "completed" until all side effects of that task are verified to exist in the database or external systems.

- **FR-009**: System MUST NOT execute any tool operation without Controller mediation - the Agent proposes, Controller validates and executes.

- **FR-010**: System MUST reload state from database at the start of every cycle, even if in-memory state appears current.

- **FR-011**: Controller MUST validate Agent output structure before processing, ensuring required fields exist and values are within acceptable ranges.

**Campaign Management**

- **FR-012**: Users MUST be able to create a new campaign by providing campaign name, target audience description, and optional context documents.

- **FR-013**: System MUST assign a unique campaign_id (UUID) to each campaign and use this ID to associate all related data (tasks, artifacts, logs).

- **FR-014**: System MUST store campaign configuration including: name, context/description, working directory path, creation timestamp, and current status (active/paused/completed).

- **FR-015**: Users MUST be able to select an existing campaign on application startup or switch between campaigns via settings.

- **FR-016**: System MUST load the selected campaign's complete state (config, tasks, artifacts, logs) when resuming, enabling continuation without conversation history.

- **FR-017**: System MUST securely store campaign environment secrets (LinkedIn credentials, API keys) per Azure Key Vault requirements defined in FR-067, with secrets never exposed in chat output, agent prompts, or local storage.

**Task Management**

- **FR-018**: System MUST break down each campaign into a comprehensive list of concrete tasks stored in the database with fields: task_id, campaign_id, description, status (pending/in_progress/completed/failed), dependencies, created_at, updated_at.

- **FR-019**: Agent MUST propose task creation when analyzing campaign goals, with Controller validating and storing tasks in database before they are considered active.

- **FR-020**: System MUST support task dependencies where a task can only be started after prerequisite tasks are completed and verified.

- **FR-021**: System MUST update task status only after verifying associated side effects exist (e.g., task "collect 50 leads" can only be marked completed after database contains 50+ lead records for this campaign).

- **FR-022**: Users MUST be able to view current task list with statuses, and see which task is currently being worked on.

- **FR-023**: System MUST log all task status changes with timestamp and reasoning in audit logs.

**Lead Collection & Management**

- **FR-024**: System MUST collect LinkedIn leads by automating LinkedIn search based on campaign target criteria.

- **FR-025**: System MUST store lead information including: lead_id, campaign_id, linkedin_profile_url (unique per campaign), name, headline, company, location, profile_notes, priority_score, created_at. System MUST support up to 1,000 leads per campaign and implement database indexes on priority_score and created_at columns for optimal query performance.

- **FR-026**: System MUST check for duplicate leads by linkedin_profile_url before insertion, updating existing records rather than creating duplicates.

- **FR-027**: System MUST extract relevant information from LinkedIn profiles including name, headline, current company, location, and any profile sections matching campaign keywords.

**Lead Prioritization**

- **FR-028**: System MUST calculate priority score for each lead using weighted heuristics based on profile data and campaign goals.

- **FR-029**: System MUST use configurable weighting factors for prioritization, defaulting to: headline keyword match (30%), company size/type match (25%), seniority level (25%), location relevance (10%), profile activity indicators (10%).

- **FR-030**: System MUST store prioritization reasoning with each lead so users can understand why a lead received its score.

- **FR-031**: Users MUST be able to view leads sorted by priority score in descending order, with UI displaying 50 leads per page using pagination controls and lazy loading to maintain responsiveness even with campaigns approaching the 1,000 lead limit.

**Message Generation**

- **FR-032**: System MUST generate LinkedIn connection request messages in Ukrainian language with warm, professional tone.

- **FR-033**: System MUST personalize each message by incorporating specific details from the lead's profile (company, role, interests) and explaining relevance to campaign offer.

- **FR-034**: System MUST respect LinkedIn connection request character limit (300 characters) when generating initial messages.

- **FR-035**: System MUST generate longer follow-up messages (up to 1000 characters) for leads who accept connection, including more detailed value proposition and call-to-action.

- **FR-036**: System MUST avoid sales-heavy language in initial connection requests, focusing on shared interests or genuine value proposition.

**LinkedIn Automation via Playwright MCP**

- **FR-037**: System MUST integrate with Playwright MCP server to automate LinkedIn browser interactions including login, search, profile viewing, connection requests, and message sending.

- **FR-038**: Controller MUST validate LinkedIn credentials exist and are current before attempting Playwright automation.

- **FR-039**: System MUST implement adaptive rate limiting for LinkedIn actions using variable delays (30-90 seconds between connection requests, 5-15 seconds between profile views), enforce daily action quotas (50 connection requests/day, 100 profile views/day, resetting at midnight user's timezone), and limit active session duration to maximum 2 hours before requiring cooling-off period.

- **FR-040**: System MUST detect and handle LinkedIn anti-automation measures including CAPTCHAs, login challenges, and rate limit warnings, pausing automation and notifying user when intervention is required.

- **FR-041**: System MUST log every Playwright action with parameters and results in audit logs before and after execution.

- **FR-042**: System MUST handle Playwright failures gracefully by logging the error, not marking tasks as completed, and proposing recovery actions based on error type.

**Chat Interface**

- **FR-043**: System MUST provide a chat interface where users can send text messages to the Agent and receive responses.

- **FR-044**: System MUST support file upload in chat interface, storing uploaded files in the campaign's working directory and making them accessible to Agent analysis.

- **FR-045**: Chat interface MUST display Controller-generated status messages (e.g., "Executing LinkedIn search...") during task execution.

- **FR-046**: System MUST ensure chat output never contains environment variables, credentials, internal tool names, or implementation details (security requirement).

- **FR-047**: Agent MUST NOT reference prior conversation in responses (no "as we discussed earlier"), instead relying on current state snapshot from database.

- **FR-048**: System MUST allow users to clear chat history without affecting campaign state or agent's ability to continue work.

**Settings Interface**

- **FR-049**: System MUST provide a settings page where users can configure LinkedIn credentials, working directory, and other campaign parameters.

- **FR-050**: Settings page MUST store credentials per Azure Key Vault requirements (FR-067) and never display credentials in plain text after initial entry. System MUST validate Azure Key Vault connectivity before accepting credential input.

- **FR-051**: Settings page MUST provide directory picker for selecting agent working directory, validating that directory exists and is writable.

- **FR-052**: System MUST allow campaign switching from settings page, loading the selected campaign's configuration and state.

- **FR-053**: Settings changes MUST take effect immediately for the next agent cycle without requiring application restart.

**Audit Logging**

- **FR-054**: System MUST create audit log entries for every significant action including: task creation/update, tool execution, database writes, file operations, and error conditions.

- **FR-055**: Audit log entries MUST include: log_id, campaign_id, task_id (if applicable), timestamp, action_type, parameters, status (initiated/completed/failed), result_details.

- **FR-056**: System MUST write audit log entry BEFORE executing the side effect it describes, updating status to "completed" or "failed" after execution.

- **FR-057**: Users MUST be able to view audit logs filtered by campaign, task, time range, or action type.

- **FR-058**: System MUST use audit logs during state recovery to verify which tasks have completed side effects and which need retry or rollback.

**Tool Integration (MCP)**

- **FR-059**: System MUST integrate with Model Context Protocol (MCP) servers for external tool access: Playwright (LinkedIn automation), Desktop-Commander (file system), Fetch/Exa (web browsing), Supabase (database). MCP server executables MUST be bundled with the application installer and automatically started as background processes on application launch, with lifecycle managed by the Controller.

- **FR-060**: Controller MUST mediate all tool access - Agent proposes tool use, Controller validates, executes via MCP, and returns results to Agent.

- **FR-061**: System MUST verify MCP server processes are running on application startup, automatically restart failed MCP servers, and configure them with campaign-specific parameters (working directory, credentials from Azure Key Vault) loaded from campaign configuration. System MUST gracefully terminate MCP server processes on application exit.

- **FR-062**: System MUST handle MCP tool failures by logging error, notifying Agent of failure reason, and allowing Agent to propose alternative actions.

**Cross-Session Recovery**

- **FR-063**: On application startup, system MUST prompt user to select or create a campaign before any other operations.

- **FR-064**: After campaign selection, Controller MUST load complete campaign state: configuration, all tasks with statuses, all artifacts (leads, messages), and recent audit logs.

- **FR-065**: System MUST resume campaign execution from current state without requiring conversation history or prior chat context.

- **FR-066**: System MUST verify integrity of loaded state by checking for orphaned records, incomplete tasks with logged side effects, and corrupted data.

**Security & Privacy**

- **FR-067**: System MUST use Azure Key Vault for storing all sensitive data (credentials, API keys) with proper access policies, managed identities for authentication, and audit logging enabled for all secret access operations.

- **FR-068**: System MUST never include environment variables, credentials, or internal tool implementation details in chat output visible to users.

- **FR-069**: System MUST validate all user inputs (campaign name, file uploads, chat messages) to prevent injection attacks or malformed data.

- **FR-070**: System MUST restrict file system access to the campaign's designated working directory only, preventing access to other system directories.

**Desktop Application**

- **FR-071**: System MUST be implemented as a .NET MAUI 10.0 Blazor Hybrid desktop application, running on Windows, macOS, and Linux.

- **FR-072**: Application MUST start in under 3 seconds on modern hardware (measured from launch to displaying main UI).

- **FR-073**: Application MUST provide native desktop integration including window management, system tray (optional), and file system dialogs.

- **FR-074**: Application UI MUST be responsive and not freeze during long-running operations (use background tasks/threads for Controller operations).

**Observability & Monitoring**

- **FR-075**: System MUST integrate Azure Application Insights for telemetry collection including performance metrics (operation durations, memory usage, CPU utilization), error tracking with stack traces, and dependency tracking for database and MCP tool calls.

- **FR-076**: System MUST emit custom telemetry events for key operations including: campaign creation, task status changes, lead collection milestones, LinkedIn automation actions, and error conditions, with relevant context properties (campaign_id, task_id, action_type).

- **FR-077**: System MUST implement distributed tracing across MCP tool invocations, allowing end-to-end visibility of request flow from Agent proposal through Controller validation to MCP tool execution and result processing.

- **FR-078**: System MUST track and log performance metrics for critical paths: application startup time, campaign state load duration, LinkedIn action execution time, and database query performance, with alerting when metrics exceed thresholds defined in success criteria.

### Key Entities

- **Campaign**: Represents a LinkedIn outreach initiative with unique goals and configuration. Contains: campaign_id (UUID), name, description/context, target_audience_description, working_directory_path, status (active/paused/completed), created_at, updated_at. Related to: Tasks, Artifacts, AuditLogs, EnvironmentSecrets.

- **Task**: Represents a concrete unit of work within a campaign. Contains: task_id (UUID), campaign_id, description, status (pending/in_progress/completed/failed), dependencies (array of task_ids), priority_order, created_at, updated_at, completed_at. Stored in database. Related to: Campaign, AuditLogs.

- **Lead**: Represents a LinkedIn profile collected as potential outreach target. Contains: lead_id (UUID), campaign_id, linkedin_profile_url (unique per campaign), name, headline, company, location, profile_notes, priority_score, prioritization_reasoning, contacted_at (nullable), connection_status (none/pending/accepted/declined), created_at, updated_at. Related to: Campaign, OutreachMessages.

- **OutreachMessage**: Represents a message generated for or sent to a lead. Contains: message_id (UUID), lead_id, campaign_id, message_type (connection_request/follow_up/inmail), message_text, language (default: Ukrainian), generated_at, sent_at (nullable), status (draft/sent/failed). Related to: Lead, Campaign.

- **AuditLog**: Represents a logged action or event for accountability and debugging. Contains: log_id (UUID), campaign_id, task_id (nullable), timestamp, action_type (enum: task_created, task_updated, tool_executed, db_write, error, etc.), parameters (JSON), status (initiated/completed/failed), result_details (JSON), created_at. Related to: Campaign, Task.

- **EnvironmentSecret**: Represents encrypted credentials or API keys for a campaign. Contains: secret_id (UUID), campaign_id, secret_key (e.g., "linkedin_email", "linkedin_password"), secret_value_encrypted, created_at, updated_at. Never exposed in chat or agent prompts.

- **AgentState**: Represents the complete state snapshot provided to Agent each cycle (not stored as single entity, but composed from multiple sources). Contains: campaign_context, task_list_with_statuses, current_artifacts (leads, messages), recent_audit_logs, available_tools, constraints. Reconstructed from database for each cycle.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Campaign can be fully resumed from database state after application restart and chat history deletion, with Agent demonstrating complete awareness of current status without referencing prior conversation (100% success in test scenarios).

- **SC-002**: Desktop application starts and displays main UI in under 3 seconds on hardware with minimum specifications (4GB RAM, modern dual-core processor, SSD storage), with startup time tracked via Application Insights telemetry.

- **SC-003**: Every side effect (database write, LinkedIn action, file operation) has a corresponding audit log entry created BEFORE execution, with 100% compliance verified through automated tests.

- **SC-004**: Task status updates occur only after side effects are verified in database, with zero instances of tasks marked "completed" without corresponding artifacts or log entries in test scenarios.

- **SC-005**: Users can create, configure, pause, and resume campaigns without data loss, with 95% task completion rate for campaigns that run across multiple sessions.

- **SC-006**: Lead collection achieves at least 80% relevance rate (leads matching campaign criteria based on profile analysis) when tested against diverse campaign descriptions.

- **SC-007**: Lead prioritization produces rankings where top 20% of leads have demonstrably higher match quality than bottom 20% based on blind evaluation by domain experts (measured via A/B testing with actual outreach results).

- **SC-008**: Generated Ukrainian messages maintain warm professional tone and personalization, with 90% approval rate from Ukrainian native speakers in quality review.

- **SC-009**: LinkedIn automation executes actions without triggering account restrictions in 95% of test campaigns when following rate limiting guidelines (at least 30 seconds between connection requests).

- **SC-010**: Chat interface never exposes environment variables, credentials, or internal tool details, with 100% compliance verified through security audit of all chat output paths.

- **SC-011**: System handles context truncation gracefully, continuing operation when conversation history exceeds 100K tokens, with no degradation in state awareness or task completion ability.

- **SC-012**: Agent proposals are validated and rejected when violating invariants, with zero instances of invalid actions being executed in production (measured through audit log analysis).

- **SC-013**: Users report ability to "set it and forget it" - campaign continues reliably across sessions with minimal intervention required, achieving 80% user satisfaction score on reliability metric in post-deployment survey.

- **SC-014**: System recovers from database connection loss within 60 seconds (automatic retry with exponential backoff), resuming normal operation without data loss or state corruption.

- **SC-015**: File uploads complete within 10 seconds for files up to 10MB, with progress indication visible to user and proper error handling for oversized files.
