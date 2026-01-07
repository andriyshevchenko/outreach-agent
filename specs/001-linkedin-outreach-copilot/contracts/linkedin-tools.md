# LinkedIn Scraping Tool Contract (Playwright MCP)

**Version**: 1.0.0  
**MCP Server**: `playwright`  
**Tool Category**: LinkedIn Automation

---

## Tool: `linkedin_scrape_profile`

### Purpose
Scrapes LinkedIn profile data for a given profile URL, extracting structured information (name, title, company, experience, education).

### Request Schema

```json
{
  "tool": "linkedin_scrape_profile",
  "arguments": {
    "profileUrl": "string (required)",
    "fields": ["string"] // optional, defaults to ["name", "title", "company", "email"]
  }
}
```

**Arguments**:
- `profileUrl` (required): Full LinkedIn profile URL (e.g., `https://www.linkedin.com/in/johndoe/`)
- `fields` (optional): Array of field names to extract. Supported values:
  - `name` - Full name
  - `title` - Current job title/headline
  - `company` - Current company name
  - `email` - Email address (if publicly visible)
  - `location` - Geographic location
  - `experience` - Work history array
  - `education` - Education history array
  - `skills` - Skills array (up to 50)

### Response Schema

```json
{
  "success": true,
  "data": {
    "name": "John Doe",
    "title": "CTO at Example Corp",
    "company": {
      "name": "Example Corp",
      "url": "https://www.linkedin.com/company/example-corp/",
      "size": "100-200 employees"
    },
    "email": "john.doe@example.com",
    "location": "San Francisco Bay Area",
    "experience": [
      {
        "title": "CTO",
        "company": "Example Corp",
        "companyUrl": "https://www.linkedin.com/company/example-corp/",
        "startDate": "2020-01",
        "endDate": null,
        "current": true,
        "description": "Leading technical strategy..."
      }
    ],
    "education": [
      {
        "school": "Stanford University",
        "degree": "BS Computer Science",
        "startYear": 2011,
        "endYear": 2015
      }
    ],
    "skills": ["Python", "Machine Learning", "Leadership"],
    "profileUrl": "https://www.linkedin.com/in/johndoe/",
    "scrapedAt": "2026-01-07T10:30:00Z"
  },
  "metadata": {
    "executionTimeMs": 12500,
    "retryCount": 0,
    "rateLimit": {
      "quotaRemaining": 45,
      "quotaResetAt": "2026-01-08T00:00:00Z"
    }
  }
}
```

### Error Response Schema

```json
{
  "success": false,
  "error": {
    "code": "PROFILE_NOT_FOUND",
    "message": "LinkedIn profile not accessible (404 or private)",
    "details": {
      "profileUrl": "https://www.linkedin.com/in/johndoe/",
      "httpStatus": 404
    }
  },
  "metadata": {
    "executionTimeMs": 3200,
    "retryCount": 1
  }
}
```

**Error Codes**:
- `PROFILE_NOT_FOUND` - Profile URL returned 404 or is private
- `RATE_LIMIT_EXCEEDED` - Daily scraping quota (50/day) exceeded
- `SESSION_EXPIRED` - LinkedIn session cookie expired or invalid
- `TIMEOUT` - Scraping operation exceeded 30-second timeout
- `CAPTCHA_DETECTED` - LinkedIn presented CAPTCHA (stealth mode failed)

### Validation Rules

**Pre-execution validation** (Controller responsibility):
1. `profileUrl` must match regex: `^https://www\.linkedin\.com/in/[a-zA-Z0-9-]+/?$`
2. Daily quota check: Verify `quotaRemaining > 0` before invocation
3. Session duration check: If session > 2 hours, force cooldown
4. LinkedIn authentication: Verify session cookie exists in `EnvironmentSecret` table

**Post-execution validation** (FR-008):
1. `success = true` AND `data.name` is not empty
2. Lead record created in database with `ProfileData` populated
3. Task `Status` updated to `Completed` ONLY if Lead record exists

### Rate Limiting (FR-039)

**Adaptive Delays**:
- Between profile scrapes: 30-90 seconds (randomized)
- After 10 consecutive scrapes: 5-minute cooldown
- After daily quota reached (50): 24-hour cooldown

**Session Limits**:
- Max session duration: 2 hours
- After 2 hours: Force Playwright shutdown, wait 4 hours before resume

### Example Usage

**C# Controller Code**:
```csharp
public async Task<Lead> ScrapeLeadProfileAsync(string linkedInUrl)
{
    // Pre-validation
    if (!IsValidLinkedInUrl(linkedInUrl))
        throw new ArgumentException("Invalid LinkedIn URL");

    if (!await CheckDailyQuotaAsync())
        throw new InvalidOperationException("Daily scraping quota exceeded");

    // Audit log BEFORE execution (FR-007)
    var auditLog = await _db.InsertAuditLogAsync(new AuditLog
    {
        TaskId = currentTask.Id,
        Action = "linkedin_scrape_profile",
        Args = JsonDocument.Parse($"{{\"profileUrl\":\"{linkedInUrl}\"}}"),
        Status = LogStatus.Pending,
        Timestamp = DateTime.UtcNow
    });

    // Invoke MCP tool
    var request = new
    {
        tool = "linkedin_scrape_profile",
        arguments = new { profileUrl = linkedInUrl }
    };

    var response = await _mcpClient.CallToolAsync("playwright", request);

    // Post-validation (FR-008)
    if (!response.GetProperty("success").GetBoolean())
    {
        await _db.UpdateAuditLogAsync(auditLog.Id, LogStatus.Failed, response);
        throw new Exception($"Scraping failed: {response.GetProperty("error").GetProperty("message")}");
    }

    // Create Lead record (database as source of truth)
    var profileData = response.GetProperty("data");
    var lead = new Lead
    {
        CampaignId = currentTask.CampaignId,
        Name = profileData.GetProperty("name").GetString(),
        LinkedInUrl = linkedInUrl,
        Email = profileData.TryGetProperty("email", out var email) ? email.GetString() : null,
        ProfileData = JsonDocument.Parse(profileData.GetRawText()),
        ScrapedAt = DateTime.UtcNow,
        Status = LeadStatus.Scraped
    };

    await _db.InsertLeadAsync(lead);
    await _db.UpdateAuditLogAsync(auditLog.Id, LogStatus.Success, response);

    return lead;
}
```

---

## Tool: `linkedin_send_message`

### Purpose
Sends a LinkedIn direct message or connection request to a specified profile.

### Request Schema

```json
{
  "tool": "linkedin_send_message",
  "arguments": {
    "recipientUrl": "string (required)",
    "messageType": "string (required)", // "connection_request" | "direct_message"
    "messageBody": "string (required)",
    "connectionNote": "string (optional)" // Required if messageType = "connection_request"
  }
}
```

**Arguments**:
- `recipientUrl` (required): LinkedIn profile URL of recipient
- `messageType` (required): `connection_request` or `direct_message`
- `messageBody` (required): Message text (max 1,900 characters for LinkedIn)
- `connectionNote` (optional): Personalized note for connection requests (max 300 characters)

### Response Schema

```json
{
  "success": true,
  "data": {
    "messageId": "msg-abc123",
    "recipientUrl": "https://www.linkedin.com/in/johndoe/",
    "messageType": "connection_request",
    "sentAt": "2026-01-07T11:00:00Z",
    "status": "pending" // "pending" | "accepted" | "declined"
  },
  "metadata": {
    "executionTimeMs": 8500,
    "retryCount": 0,
    "rateLimit": {
      "quotaRemaining": 48,
      "quotaResetAt": "2026-01-08T00:00:00Z"
    }
  }
}
```

### Error Response Schema

```json
{
  "success": false,
  "error": {
    "code": "MESSAGE_LIMIT_EXCEEDED",
    "message": "LinkedIn weekly messaging limit reached (100 messages/week)",
    "details": {
      "recipientUrl": "https://www.linkedin.com/in/johndoe/",
      "weeklyQuotaResetAt": "2026-01-13T00:00:00Z"
    }
  }
}
```

**Error Codes**:
- `MESSAGE_LIMIT_EXCEEDED` - Weekly LinkedIn messaging limit (100) reached
- `NOT_CONNECTED` - Direct message requires existing connection
- `PROFILE_NOT_FOUND` - Recipient profile not accessible
- `RATE_LIMIT_EXCEEDED` - Daily messaging quota (50) exceeded
- `SESSION_EXPIRED` - LinkedIn session invalid

### Validation Rules

**Pre-execution validation**:
1. Daily quota: ≤ 50 messages/day (FR-039)
2. Message length: ≤ 1,900 characters
3. Connection note length: ≤ 300 characters (if applicable)
4. Verify Lead record exists in database

**Post-execution validation** (FR-008):
1. `success = true` AND `data.messageId` is not empty
2. `OutreachMessage` record created with `Status = Sent`
3. Lead `Status` updated to `Contacted`

### Rate Limiting

**Adaptive Delays**:
- Between messages: 45-120 seconds (randomized)
- After 5 consecutive messages: 10-minute cooldown

### Example Usage

**C# Controller Code**:
```csharp
public async Task<OutreachMessage> SendLinkedInMessageAsync(Guid leadId, string messageBody)
{
    var lead = await _db.GetLeadAsync(leadId);
    
    // Pre-validation
    if (!await CheckDailyQuotaAsync())
        throw new InvalidOperationException("Daily messaging quota exceeded");

    if (messageBody.Length > 1900)
        throw new ArgumentException("Message exceeds LinkedIn limit (1,900 characters)");

    // Audit log BEFORE execution
    var auditLog = await _db.InsertAuditLogAsync(new AuditLog
    {
        TaskId = currentTask.Id,
        Action = "linkedin_send_message",
        Args = JsonDocument.Parse($"{{\"recipientUrl\":\"{lead.LinkedInUrl}\",\"messageBody\":\"{messageBody}\"}}"),
        Status = LogStatus.Pending
    });

    // Invoke MCP tool
    var request = new
    {
        tool = "linkedin_send_message",
        arguments = new
        {
            recipientUrl = lead.LinkedInUrl,
            messageType = "connection_request",
            messageBody = messageBody,
            connectionNote = $"Hi {lead.Name}, I'd like to connect..."
        }
    };

    var response = await _mcpClient.CallToolAsync("playwright", request);

    if (!response.GetProperty("success").GetBoolean())
    {
        await _db.UpdateAuditLogAsync(auditLog.Id, LogStatus.Failed, response);
        throw new Exception("Message sending failed");
    }

    // Create OutreachMessage record
    var outreachMessage = new OutreachMessage
    {
        TaskId = currentTask.Id,
        LeadId = leadId,
        Type = MessageType.LinkedInConnectionRequest,
        Status = MessageStatus.Sent,
        Body = messageBody,
        SentAt = DateTime.UtcNow,
        Metadata = JsonDocument.Parse(response.GetProperty("data").GetRawText())
    };

    await _db.InsertOutreachMessageAsync(outreachMessage);
    await _db.UpdateLeadStatusAsync(leadId, LeadStatus.Contacted);
    await _db.UpdateAuditLogAsync(auditLog.Id, LogStatus.Success, response);

    return outreachMessage;
}
```

---

## Security and Compliance

### Authentication
- LinkedIn session managed via `EnvironmentSecret` table → Azure Key Vault
- Session cookie refreshed every 24 hours via background job
- Manual login fallback if automated refresh fails

### Data Privacy
- Scraped profile data retained for 90 days max (GDPR compliance)
- User consent required before storing email addresses
- PII encryption at rest using AES-256

### Anti-Detection Measures (FR-041)
- Playwright stealth mode: `navigator.webdriver` override, WebGL randomization
- Human behavior simulation: Mouse movements, scroll events, typing delays
- IP rotation: Optional proxy support for enterprise deployments

### Audit Trail (FR-056)
- All tool invocations logged with timestamps, args, results
- Immutable audit logs (database trigger prevents updates)
- Retention: 1 year

---

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public async Task ScrapeProfile_ValidUrl_ReturnsLeadData()
{
    // Arrange
    var mockMcpClient = new Mock<IMcpClient>();
    mockMcpClient.Setup(m => m.CallToolAsync("playwright", It.IsAny<object>()))
        .ReturnsAsync(JsonDocument.Parse("{\"success\":true,\"data\":{\"name\":\"Test User\"}}"));

    // Act
    var result = await _controller.ScrapeLeadProfileAsync("https://www.linkedin.com/in/testuser/");

    // Assert
    Assert.Equal("Test User", result.Name);
    Assert.NotNull(result.ProfileData);
}
```

### Integration Tests
- Test with LinkedIn test account (sandbox environment)
- Verify rate limiting enforcement
- Test session expiration handling

### E2E Tests
- Full campaign execution with 5 test leads
- Verify database state after each task
- Test error recovery (network failures, CAPTCHA)
