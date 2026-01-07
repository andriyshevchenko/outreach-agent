# Desktop Commander Tools Contract (File System MCP)

**Version**: 1.0.0  
**MCP Server**: `desktop-commander`  
**Tool Category**: File System Operations (Campaign Export/Import)

---

## Tool: `export_campaign_data`

### Purpose
Exports campaign data (leads, messages, analytics) to CSV/JSON files for offline analysis or backup.

### Request Schema

```json
{
  "tool": "export_campaign_data",
  "arguments": {
    "campaignId": "string (required)",
    "exportFormat": "string (required)", // "csv" | "json"
    "outputPath": "string (required)",
    "includeLeads": true,
    "includeMessages": true,
    "includeAnalytics": true
  }
}
```

**Arguments**:
- `campaignId` (required): UUID of campaign to export
- `exportFormat` (required): `csv` or `json`
- `outputPath` (required): Absolute path to output directory (e.g., `C:\Exports\`)
- `includeLeads` (optional): Include leads table (default: true)
- `includeMessages` (optional): Include outreach messages (default: true)
- `includeAnalytics` (optional): Include campaign analytics summary (default: true)

### Response Schema

```json
{
  "success": true,
  "data": {
    "exportId": "export-abc-123",
    "campaignName": "CTO Outreach Q1",
    "exportedFiles": [
      "C:\\Exports\\cto-outreach-q1-leads.csv",
      "C:\\Exports\\cto-outreach-q1-messages.csv",
      "C:\\Exports\\cto-outreach-q1-analytics.json"
    ],
    "totalRecords": 250,
    "exportedAt": "2026-01-07T14:00:00Z"
  },
  "metadata": {
    "executionTimeMs": 3500,
    "fileSize": "2.5 MB"
  }
}
```

### CSV Export Format

**Leads CSV** (`{campaign-name}-leads.csv`):
```csv
Lead ID,Name,LinkedIn URL,Email,Title,Company,Status,Scraped At,Created At
lead-001,John Doe,https://linkedin.com/in/johndoe/,john@example.com,CTO,Example Corp,Scraped,2026-01-05T10:00:00Z,2026-01-05T09:30:00Z
lead-002,Jane Smith,https://linkedin.com/in/janesmith/,jane@corp.com,VP Engineering,Corp Inc,Contacted,2026-01-05T11:00:00Z,2026-01-05T09:30:00Z
```

**Messages CSV** (`{campaign-name}-messages.csv`):
```csv
Message ID,Lead Name,Type,Subject,Status,Sent At,Opened At,Clicked At
msg-001,John Doe,LinkedInConnectionRequest,,Sent,2026-01-06T10:00:00Z,,
msg-002,Jane Smith,Email,Quick question about Corp Inc,Opened,2026-01-06T11:00:00Z,2026-01-06T12:30:00Z,
```

**Analytics JSON** (`{campaign-name}-analytics.json`):
```json
{
  "campaignId": "campaign-abc",
  "campaignName": "CTO Outreach Q1",
  "summary": {
    "totalLeads": 250,
    "leadsScraped": 180,
    "leadsContacted": 120,
    "repliesReceived": 15,
    "conversionRate": 0.125
  },
  "dailyActivity": [
    {
      "date": "2026-01-05",
      "leadsScraped": 45,
      "messagesSent": 30
    },
    {
      "date": "2026-01-06",
      "leadsScraped": 50,
      "messagesSent": 35
    }
  ],
  "performance": {
    "avgScrapeTimeMs": 12000,
    "avgMessageDeliveryTimeMs": 3500
  }
}
```

### Validation Rules

**Pre-execution validation**:
1. `campaignId` must exist in database
2. `outputPath` must be writable directory
3. User has permission to write to output path

**Post-execution validation**:
1. All specified files created successfully
2. File row counts match database record counts
3. No data loss during export

### Example Usage

**C# Controller Code**:
```csharp
public async Task<ExportResult> ExportCampaignDataAsync(Guid campaignId, string outputPath)
{
    var campaign = await _db.GetCampaignAsync(campaignId);

    // Pre-validation
    if (!Directory.Exists(outputPath))
        Directory.CreateDirectory(outputPath);

    // Audit log
    var auditLog = await _db.InsertAuditLogAsync(new AuditLog
    {
        TaskId = Guid.NewGuid(), // Special "export" task
        Action = "export_campaign_data",
        Args = JsonDocument.Parse($"{{\"campaignId\":\"{campaignId}\",\"outputPath\":\"{outputPath}\"}}"),
        Status = LogStatus.Pending
    });

    // Invoke Desktop Commander MCP tool
    var request = new
    {
        tool = "export_campaign_data",
        arguments = new
        {
            campaignId = campaignId.ToString(),
            exportFormat = "csv",
            outputPath = outputPath,
            includeLeads = true,
            includeMessages = true,
            includeAnalytics = true
        }
    };

    var response = await _mcpClient.CallToolAsync("desktop-commander", request);

    if (!response.GetProperty("success").GetBoolean())
    {
        await _db.UpdateAuditLogAsync(auditLog.Id, LogStatus.Failed, response);
        throw new Exception("Export failed");
    }

    // Verify files exist
    var exportedFiles = response.GetProperty("data").GetProperty("exportedFiles").EnumerateArray()
        .Select(f => f.GetString()).ToList();

    foreach (var file in exportedFiles)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException($"Expected export file not found: {file}");
    }

    await _db.UpdateAuditLogAsync(auditLog.Id, LogStatus.Success, response);

    return new ExportResult
    {
        ExportedFiles = exportedFiles,
        TotalRecords = response.GetProperty("data").GetProperty("totalRecords").GetInt32()
    };
}
```

---

## Tool: `import_leads_from_csv`

### Purpose
Imports leads from CSV file into specified campaign, creating Lead and Task records.

### Request Schema

```json
{
  "tool": "import_leads_from_csv",
  "arguments": {
    "campaignId": "string (required)",
    "csvFilePath": "string (required)",
    "columnMapping": {
      "name": "Full Name",
      "linkedInUrl": "LinkedIn Profile",
      "email": "Email Address",
      "title": "Job Title",
      "company": "Company Name"
    },
    "createScrapeTask": true
  }
}
```

**Arguments**:
- `campaignId` (required): UUID of target campaign
- `csvFilePath` (required): Absolute path to CSV file
- `columnMapping` (required): Map database fields to CSV column headers
- `createScrapeTask` (optional): Automatically create `ScrapeProfile` tasks for new leads (default: true)

### Response Schema

```json
{
  "success": true,
  "data": {
    "importId": "import-xyz-789",
    "campaignId": "campaign-abc",
    "leadsImported": 100,
    "leadsSkipped": 5,
    "tasksCreated": 100,
    "importedAt": "2026-01-07T15:00:00Z",
    "errors": [
      {
        "row": 23,
        "error": "Invalid LinkedIn URL format"
      },
      {
        "row": 47,
        "error": "Duplicate LinkedIn URL (already exists)"
      }
    ]
  },
  "metadata": {
    "executionTimeMs": 8500,
    "csvRowCount": 105
  }
}
```

### CSV Import Format

**Expected CSV Structure**:
```csv
Full Name,LinkedIn Profile,Email Address,Job Title,Company Name
John Doe,https://www.linkedin.com/in/johndoe/,john@example.com,CTO,Example Corp
Jane Smith,https://www.linkedin.com/in/janesmith/,jane@corp.com,VP Engineering,Corp Inc
```

### Validation Rules

**Pre-execution validation**:
1. CSV file exists and is readable
2. CSV has header row matching `columnMapping` keys
3. Required columns present: `name`, `linkedInUrl`
4. Campaign exists and is in `Draft` or `Active` status

**Row-level validation** (during import):
1. `linkedInUrl` must match regex: `^https://www\.linkedin\.com/in/[a-zA-Z0-9-]+/?$`
2. `email` must be valid email format (if present)
3. Duplicate `linkedInUrl` across database: Skip row, log error

**Post-execution validation**:
1. `leadsImported` + `leadsSkipped` = `csvRowCount` - 1 (header row)
2. All valid leads have corresponding `Task` records (if `createScrapeTask = true`)

### Example Usage

**C# Controller Code**:
```csharp
public async Task<ImportResult> ImportLeadsFromCsvAsync(Guid campaignId, string csvFilePath)
{
    // Pre-validation
    if (!File.Exists(csvFilePath))
        throw new FileNotFoundException($"CSV file not found: {csvFilePath}");

    var campaign = await _db.GetCampaignAsync(campaignId);
    if (campaign.Status == CampaignStatus.Completed)
        throw new InvalidOperationException("Cannot import leads into completed campaign");

    // Audit log
    var auditLog = await _db.InsertAuditLogAsync(new AuditLog
    {
        TaskId = Guid.NewGuid(),
        Action = "import_leads_from_csv",
        Args = JsonDocument.Parse($"{{\"campaignId\":\"{campaignId}\",\"csvFilePath\":\"{csvFilePath}\"}}"),
        Status = LogStatus.Pending
    });

    // Invoke Desktop Commander MCP tool
    var request = new
    {
        tool = "import_leads_from_csv",
        arguments = new
        {
            campaignId = campaignId.ToString(),
            csvFilePath = csvFilePath,
            columnMapping = new
            {
                name = "Full Name",
                linkedInUrl = "LinkedIn Profile",
                email = "Email Address",
                title = "Job Title",
                company = "Company Name"
            },
            createScrapeTask = true
        }
    };

    var response = await _mcpClient.CallToolAsync("desktop-commander", request);

    if (!response.GetProperty("success").GetBoolean())
    {
        await _db.UpdateAuditLogAsync(auditLog.Id, LogStatus.Failed, response);
        throw new Exception("Import failed");
    }

    var data = response.GetProperty("data");
    var leadsImported = data.GetProperty("leadsImported").GetInt32();
    var leadsSkipped = data.GetProperty("leadsSkipped").GetInt32();

    await _db.UpdateAuditLogAsync(auditLog.Id, LogStatus.Success, response);

    return new ImportResult
    {
        LeadsImported = leadsImported,
        LeadsSkipped = leadsSkipped,
        Errors = data.GetProperty("errors").EnumerateArray()
            .Select(e => $"Row {e.GetProperty("row").GetInt32()}: {e.GetProperty("error").GetString()}")
            .ToList()
    };
}
```

---

## Security Considerations

### File Access Permissions
- Desktop Commander MCP server runs with user-level permissions (no elevation)
- File operations restricted to user-writable directories (Documents, Downloads, Desktop)
- No access to system directories or Program Files

### Path Validation
```csharp
public bool IsPathSafe(string path)
{
    // Normalize path
    var fullPath = Path.GetFullPath(path);

    // Allow only user directories
    var allowedPaths = new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
    };

    return allowedPaths.Any(allowed => fullPath.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
}
```

### CSV Injection Prevention
- Sanitize all CSV output: Prefix `=`, `+`, `-`, `@` with single quote `'`
- Validate imported CSV for formula injection patterns

---

## Testing Strategy

### Unit Tests
```csharp
[Fact]
public async Task ExportCampaignData_ValidRequest_CreatesFiles()
{
    // Arrange
    var campaignId = Guid.NewGuid();
    var outputPath = Path.Combine(Path.GetTempPath(), "test-export");

    // Act
    var result = await _controller.ExportCampaignDataAsync(campaignId, outputPath);

    // Assert
    Assert.True(result.ExportedFiles.All(File.Exists));
    Assert.True(result.TotalRecords > 0);
}

[Fact]
public async Task ImportLeadsFromCsv_ValidCsv_ImportsLeads()
{
    // Arrange
    var csvContent = "Full Name,LinkedIn Profile\nJohn Doe,https://www.linkedin.com/in/johndoe/";
    var csvPath = Path.Combine(Path.GetTempPath(), "test-import.csv");
    File.WriteAllText(csvPath, csvContent);

    // Act
    var result = await _controller.ImportLeadsFromCsvAsync(campaignId, csvPath);

    // Assert
    Assert.Equal(1, result.LeadsImported);
    Assert.Empty(result.Errors);
}
```

### Integration Tests
- Test with large CSV files (10,000+ rows)
- Verify duplicate detection
- Test error handling (malformed CSV, invalid paths)

### E2E Tests
- Export campaign → modify CSV → re-import
- Verify data integrity across export/import cycle
- Test Windows-specific path handling (UNC paths, drive letters)
