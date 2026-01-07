# Email Tool Contract (SMTP)

**Version**: 1.0.0  
**Service**: SMTP Email (SendGrid / Azure Communication Services)  
**Tool Category**: Email Outreach

---

## Tool: `send_email`

### Purpose
Sends personalized email to lead with template-based content and tracking capabilities.

### Request Schema

```json
{
  "tool": "send_email",
  "arguments": {
    "recipientEmail": "string (required)",
    "recipientName": "string (required)",
    "subject": "string (required)",
    "bodyHtml": "string (required)",
    "bodyText": "string (optional)",
    "fromName": "string (optional)",
    "fromEmail": "string (optional)",
    "replyTo": "string (optional)",
    "trackOpens": true,
    "trackClicks": true
  }
}
```

**Arguments**:
- `recipientEmail` (required): Valid email address of lead
- `recipientName` (required): Full name for personalization
- `subject` (required): Email subject line (max 200 characters)
- `bodyHtml` (required): HTML email body
- `bodyText` (optional): Plain text fallback
- `fromName` (optional): Sender name (defaults to campaign owner)
- `fromEmail` (optional): Sender email (defaults to configured SMTP identity)
- `replyTo` (optional): Reply-to email address
- `trackOpens` (optional): Enable open tracking (default: true)
- `trackClicks` (optional): Enable click tracking (default: true)

### Response Schema

```json
{
  "success": true,
  "data": {
    "messageId": "email-msg-xyz789",
    "recipientEmail": "john.doe@example.com",
    "subject": "Quick question about Example Corp",
    "sentAt": "2026-01-07T12:00:00Z",
    "status": "queued", // "queued" | "delivered" | "bounced" | "opened" | "clicked"
    "trackingEnabled": {
      "opens": true,
      "clicks": true
    }
  },
  "metadata": {
    "executionTimeMs": 1200,
    "smtpProvider": "sendgrid",
    "retryCount": 0
  }
}
```

### Error Response Schema

```json
{
  "success": false,
  "error": {
    "code": "INVALID_EMAIL",
    "message": "Recipient email address is invalid or disposable",
    "details": {
      "recipientEmail": "invalid@disposable-domain.com",
      "reason": "Disposable email domain blocked"
    }
  }
}
```

**Error Codes**:
- `INVALID_EMAIL` - Email format invalid or disposable domain
- `BOUNCE_DETECTED` - Email previously bounced (stored in database)
- `RATE_LIMIT_EXCEEDED` - Daily email quota (100/day) exceeded
- `SPAM_FILTER_RISK` - Content flagged by spam analysis
- `SMTP_AUTH_FAILED` - SMTP credentials invalid

### Validation Rules

**Pre-execution validation**:
1. Email format: Valid per RFC 5322
2. Disposable email check: Block known disposable domains (10minutemail.com, etc.)
3. Bounce history: Check database for previous bounces
4. Daily quota: ≤ 100 emails/day
5. Content validation: Subject + body length < 50KB

**Post-execution validation** (FR-008):
1. `success = true` AND `data.messageId` is not empty
2. `OutreachMessage` record created with `Type = Email`, `Status = Sent`
3. Lead `Status` updated to `Contacted`

### Rate Limiting

**Delays**:
- Between emails: 10-30 seconds (randomized)
- Daily limit: 100 emails (configurable per campaign)

### Example Usage

**C# Controller Code**:
```csharp
public async Task<OutreachMessage> SendEmailAsync(Guid leadId, string subject, string bodyHtml)
{
    var lead = await _db.GetLeadAsync(leadId);

    // Pre-validation
    if (!IsValidEmail(lead.Email))
        throw new ArgumentException("Invalid email address");

    if (await HasBouncedAsync(lead.Email))
        throw new InvalidOperationException("Email previously bounced, skipping");

    if (!await CheckEmailQuotaAsync())
        throw new InvalidOperationException("Daily email quota (100) exceeded");

    // Audit log BEFORE execution
    var auditLog = await _db.InsertAuditLogAsync(new AuditLog
    {
        TaskId = currentTask.Id,
        Action = "send_email",
        Args = JsonDocument.Parse($"{{\"recipientEmail\":\"{lead.Email}\",\"subject\":\"{subject}\"}}"),
        Status = LogStatus.Pending
    });

    // Invoke email service
    var request = new
    {
        tool = "send_email",
        arguments = new
        {
            recipientEmail = lead.Email,
            recipientName = lead.Name,
            subject = subject,
            bodyHtml = bodyHtml,
            trackOpens = true,
            trackClicks = true
        }
    };

    var response = await _emailService.SendEmailAsync(request);

    if (!response.GetProperty("success").GetBoolean())
    {
        await _db.UpdateAuditLogAsync(auditLog.Id, LogStatus.Failed, response);
        throw new Exception("Email sending failed");
    }

    // Create OutreachMessage record
    var outreachMessage = new OutreachMessage
    {
        TaskId = currentTask.Id,
        LeadId = leadId,
        Type = MessageType.Email,
        Status = MessageStatus.Sent,
        Subject = subject,
        Body = bodyHtml,
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

## Email Templates

### Template: Initial Outreach

**Subject**: `Quick question about {{companyName}}`

**Body (HTML)**:
```html
Hi {{firstName}},

I noticed your work as {{title}} at {{companyName}} and wanted to reach out about [value proposition].

[Personalized paragraph based on recent activity or company news]

Would you be open to a quick 15-minute call next week to discuss?

Best regards,
[Your Name]
[Your Title]
[Your Company]
```

### Template: Follow-Up

**Subject**: `Following up - {{companyName}}`

**Body (HTML)**:
```html
Hi {{firstName}},

I wanted to follow up on my previous email about [topic].

I understand you're busy, but I believe [specific benefit] could be valuable for {{companyName}}.

Are you available for a brief call this week?

Thanks,
[Your Name]
```

### Template Variables

- `{{firstName}}` - Lead's first name
- `{{lastName}}` - Lead's last name
- `{{title}}` - Lead's job title
- `{{companyName}}` - Lead's company name
- `{{industry}}` - Company industry
- `{{location}}` - Lead's location
- `{{recentPost}}` - Lead's recent LinkedIn post topic (if scraped)

---

## Tracking and Analytics

### Open Tracking
- Implemented via 1x1 pixel: `<img src="https://track.example.com/open/{messageId}" width="1" height="1" />`
- Webhook updates `OutreachMessage.Status = Opened` when pixel loads

### Click Tracking
- Rewrite all links: `https://track.example.com/click/{messageId}/{linkId}`
- Webhook logs click events and updates Lead engagement score

### Bounce Handling
- Webhook endpoint: `/webhooks/email/bounce`
- On hard bounce: Mark `Lead.Status = Disqualified`, block future emails
- On soft bounce: Retry up to 3 times over 24 hours

---

## SMTP Configuration

### SendGrid (Recommended)

**appsettings.json**:
```json
{
  "Email": {
    "Provider": "SendGrid",
    "ApiKey": "{KeyVault:sendgrid-api-key}",
    "FromEmail": "outreach@example.com",
    "FromName": "Example Corp Outreach Team",
    "TrackingDomain": "track.example.com",
    "WebhookUrl": "https://app.example.com/webhooks/email"
  }
}
```

**C# Integration**:
```csharp
using SendGrid;
using SendGrid.Helpers.Mail;

public class SendGridEmailService
{
    private readonly SendGridClient _client;

    public SendGridEmailService(IConfiguration config)
    {
        var apiKey = _secretManager.GetSecretAsync("sendgrid-api-key").Result;
        _client = new SendGridClient(apiKey);
    }

    public async Task<JsonElement> SendEmailAsync(EmailRequest request)
    {
        var from = new EmailAddress(request.FromEmail, request.FromName);
        var to = new EmailAddress(request.RecipientEmail, request.RecipientName);
        var msg = MailHelper.CreateSingleEmail(from, to, request.Subject, request.BodyText, request.BodyHtml);

        // Enable tracking
        msg.SetClickTracking(request.TrackClicks, request.TrackClicks);
        msg.SetOpenTracking(request.TrackOpens);

        var response = await _client.SendEmailAsync(msg);
        return JsonDocument.Parse($"{{\"success\":{response.IsSuccessStatusCode},\"messageId\":\"{response.Headers.GetValues(\"X-Message-Id\").FirstOrDefault()}\"}}").RootElement;
    }
}
```

---

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public async Task SendEmail_ValidRequest_ReturnsSuccess()
{
    // Arrange
    var mockEmailService = new Mock<IEmailService>();
    mockEmailService.Setup(s => s.SendEmailAsync(It.IsAny<EmailRequest>()))
        .ReturnsAsync(JsonDocument.Parse("{\"success\":true,\"messageId\":\"test-msg-123\"}").RootElement);

    // Act
    var result = await _controller.SendEmailAsync(leadId, "Test Subject", "<p>Test Body</p>");

    // Assert
    Assert.Equal(MessageStatus.Sent, result.Status);
    Assert.NotNull(result.Metadata);
}
```

### Integration Tests
- Test with SendGrid sandbox (real API calls, no delivery)
- Verify webhook processing (bounce, open, click events)
- Test rate limiting enforcement

### E2E Tests
- Send emails to test accounts
- Verify tracking pixel and click redirects
- Test bounce handling and retry logic
