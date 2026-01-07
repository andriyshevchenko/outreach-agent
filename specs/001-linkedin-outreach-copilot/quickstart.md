# Quickstart Guide: LinkedIn Outreach Copilot Agent

**Feature**: LinkedIn Outreach Copilot Agent  
**Version**: 1.0.0  
**Target Audience**: Developers setting up local development environment

---

## Prerequisites

### Required Software

| Tool | Version | Download Link | Purpose |
|------|---------|---------------|---------|
| .NET SDK | 10.0+ | https://dot.net/download | MAUI Blazor Hybrid development |
| Visual Studio 2022 | 17.12+ | https://visualstudio.microsoft.com/vs/ | IDE with MAUI workload |
| Node.js | 20.x LTS | https://nodejs.org/ | MCP server executables (Playwright) |
| PostgreSQL | 16.x | https://www.postgresql.org/download/ | Local database (optional) |
| Azure CLI | 2.65+ | https://learn.microsoft.com/cli/azure/install-azure-cli | Azure resource provisioning |
| Supabase CLI | 1.200+ | https://supabase.com/docs/guides/cli | Database schema management |
| Git | Latest | https://git-scm.com/ | Version control |

### Visual Studio Workloads

Install via Visual Studio Installer:
- `.NET Multi-platform App UI development` (MAUI workload)
- `ASP.NET and web development`
- `.NET desktop development`

**Verify Installation**:
```powershell
dotnet --version  # Should output 10.0.x
dotnet workload list  # Should include: maui-windows, maui-maccatalyst, maui-ios, maui-android
```

### Azure Subscription

**Required Azure Services**:
1. **Azure Key Vault** - Secret storage for LinkedIn credentials, API keys
2. **Azure Application Insights** - Telemetry and distributed tracing
3. **Azure OpenAI Service** - LLM agent (GPT-4 Turbo)

**Cost Estimate** (development environment):
- Key Vault: ~$0.03/transaction (negligible for dev)
- Application Insights: First 5GB free, then $2.88/GB
- Azure OpenAI: GPT-4 Turbo ~$0.01/1K tokens input, ~$0.03/1K tokens output

### Supabase Project

**Sign Up**: https://supabase.com (free tier sufficient for development)

**Create Project**:
1. Go to https://supabase.com/dashboard
2. Click "New Project"
3. Name: `outreach-agent-dev`
4. Database Password: Generate strong password (save to Key Vault later)
5. Region: Choose nearest to your location
6. Wait 2-3 minutes for provisioning

---

## Setup Instructions

### Step 1: Clone Repository

```powershell
# Navigate to workspace directory
cd C:\src

# Clone repository (replace with actual repo URL when available)
git clone https://github.com/your-org/outreach-agent.git
cd outreach-agent
```

**Expected Directory Structure**:
```
C:\src\outreach-agent\
├── .specify\
│   ├── memory\
│   │   └── constitution.md
│   └── scripts\
│       └── powershell\
│           ├── setup-plan.ps1
│           └── update-agent-context.ps1
├── specs\
│   └── 001-linkedin-outreach-copilot\
│       ├── spec.md
│       ├── plan.md
│       ├── research.md
│       ├── data-model.md
│       ├── checklists\
│       └── contracts\
└── src\  (to be created)
    ├── OutreachAgent.Core\
    ├── OutreachAgent.Agent\
    ├── OutreachAgent.MCP\
    ├── OutreachAgent.UI\
    └── OutreachAgent.Tests\
```

---

### Step 2: Provision Azure Resources

**Login to Azure CLI**:
```powershell
az login
az account set --subscription "<your-subscription-id>"
```

**Create Resource Group**:
```powershell
$resourceGroup = "rg-outreach-agent-dev"
$location = "eastus"

az group create --name $resourceGroup --location $location
```

**Create Azure Key Vault**:
```powershell
$keyVaultName = "kv-outreach-dev-$(Get-Random -Maximum 9999)"
az keyvault create `
    --name $keyVaultName `
    --resource-group $resourceGroup `
    --location $location `
    --enable-rbac-authorization true

# Get current user object ID
$userObjectId = az ad signed-in-user show --query id --output tsv

# Assign Key Vault Secrets Officer role
az role assignment create `
    --role "Key Vault Secrets Officer" `
    --assignee $userObjectId `
    --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$resourceGroup/providers/Microsoft.KeyVault/vaults/$keyVaultName"
```

**Create Application Insights**:
```powershell
$appInsightsName = "ai-outreach-dev"
az monitor app-insights component create `
    --app $appInsightsName `
    --location $location `
    --resource-group $resourceGroup `
    --application-type web

# Get connection string
$appInsightsConnectionString = az monitor app-insights component show `
    --app $appInsightsName `
    --resource-group $resourceGroup `
    --query connectionString `
    --output tsv

Write-Host "Application Insights Connection String: $appInsightsConnectionString"
```

**Create Azure OpenAI Service**:
```powershell
$openAiName = "openai-outreach-dev"
az cognitiveservices account create `
    --name $openAiName `
    --resource-group $resourceGroup `
    --location $location `
    --kind OpenAI `
    --sku S0 `
    --custom-domain $openAiName

# Deploy GPT-4 Turbo model
az cognitiveservices account deployment create `
    --name $openAiName `
    --resource-group $resourceGroup `
    --deployment-name gpt-4-turbo `
    --model-name gpt-4 `
    --model-version turbo-2024-04-09 `
    --model-format OpenAI `
    --sku-capacity 10 `
    --sku-name "Standard"

# Get API key
$openAiKey = az cognitiveservices account keys list `
    --name $openAiName `
    --resource-group $resourceGroup `
    --query key1 `
    --output tsv

$openAiEndpoint = az cognitiveservices account show `
    --name $openAiName `
    --resource-group $resourceGroup `
    --query properties.endpoint `
    --output tsv

Write-Host "Azure OpenAI Endpoint: $openAiEndpoint"
Write-Host "Azure OpenAI Key: $openAiKey"
```

**Store Secrets in Key Vault**:
```powershell
# Supabase connection string (get from Supabase dashboard)
$supabaseUrl = "https://<your-project>.supabase.co"
$supabaseAnonKey = "<your-anon-key>"
$supabaseDatabaseUrl = "postgres://postgres.[your-project]:[password]@aws-0-[region].pooler.supabase.com:6543/postgres?pgbouncer=true"

az keyvault secret set --vault-name $keyVaultName --name "supabase-url" --value $supabaseUrl
az keyvault secret set --vault-name $keyVaultName --name "supabase-anon-key" --value $supabaseAnonKey
az keyvault secret set --vault-name $keyVaultName --name "supabase-database-url" --value $supabaseDatabaseUrl

# Azure OpenAI credentials
az keyvault secret set --vault-name $keyVaultName --name "openai-api-key" --value $openAiKey
az keyvault secret set --vault-name $keyVaultName --name "openai-endpoint" --value $openAiEndpoint

# LinkedIn credentials (manually add after first login)
# az keyvault secret set --vault-name $keyVaultName --name "linkedin-session-cookie" --value "<cookie-value>"
```

---

### Step 3: Initialize Supabase Database

**Login to Supabase CLI**:
```powershell
supabase login
```

**Link Local Project to Supabase**:
```powershell
cd C:\src\outreach-agent
supabase link --project-ref <your-project-ref>
```

**Create Database Schema** (from `data-model.md`):

Create file: `supabase\migrations\20260107000001_initial_schema.sql`

```sql
-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Campaign table
CREATE TABLE campaigns (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name VARCHAR(200) NOT NULL,
    description TEXT,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    status VARCHAR(50) NOT NULL CHECK (status IN ('Draft', 'Active', 'Paused', 'Completed', 'Archived')),
    config JSONB NOT NULL DEFAULT '{}'
);

CREATE INDEX idx_campaigns_status ON campaigns(status);
CREATE INDEX idx_campaigns_created_at ON campaigns(created_at);

-- Lead table
CREATE TABLE leads (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    name VARCHAR(200) NOT NULL,
    linkedin_url VARCHAR(500) NOT NULL UNIQUE,
    email VARCHAR(320),
    profile_data JSONB,
    scraped_at TIMESTAMP,
    status VARCHAR(50) NOT NULL CHECK (status IN ('New', 'Scraped', 'Contacted', 'Replied', 'Converted', 'Disqualified')),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_leads_campaign_status ON leads(campaign_id, status);
CREATE INDEX idx_leads_email ON leads(email);

-- Task table
CREATE TABLE tasks (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    campaign_id UUID NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
    lead_id UUID NOT NULL REFERENCES leads(id) ON DELETE CASCADE,
    type VARCHAR(50) NOT NULL CHECK (type IN ('ScrapeProfile', 'SendLinkedInMessage', 'SendEmail', 'WaitCooldown')),
    status VARCHAR(50) NOT NULL CHECK (status IN ('Pending', 'InProgress', 'Completed', 'Failed', 'Cancelled')),
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMP,
    input JSONB NOT NULL DEFAULT '{}',
    output JSONB,
    error_message TEXT
);

CREATE INDEX idx_tasks_campaign_status ON tasks(campaign_id, status);
CREATE INDEX idx_tasks_lead_id ON tasks(lead_id);
CREATE INDEX idx_tasks_created_at ON tasks(created_at);

-- OutreachMessage table
CREATE TABLE outreach_messages (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    task_id UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    lead_id UUID NOT NULL REFERENCES leads(id) ON DELETE CASCADE,
    type VARCHAR(50) NOT NULL CHECK (type IN ('LinkedInConnectionRequest', 'LinkedInDirectMessage', 'Email')),
    status VARCHAR(50) NOT NULL CHECK (status IN ('Draft', 'Pending', 'Sent', 'Failed', 'Replied')),
    subject VARCHAR(500),
    body TEXT NOT NULL,
    sent_at TIMESTAMP,
    metadata JSONB NOT NULL DEFAULT '{}'
);

CREATE INDEX idx_outreach_messages_lead_status ON outreach_messages(lead_id, status);
CREATE INDEX idx_outreach_messages_sent_at ON outreach_messages(sent_at);

-- AuditLog table
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    task_id UUID NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    action VARCHAR(200) NOT NULL,
    args JSONB NOT NULL DEFAULT '{}',
    result JSONB,
    timestamp TIMESTAMP NOT NULL DEFAULT NOW(),
    status VARCHAR(50) NOT NULL CHECK (status IN ('Pending', 'Success', 'Failed')),
    error_message TEXT
);

CREATE INDEX idx_audit_logs_task_timestamp ON audit_logs(task_id, timestamp);
CREATE INDEX idx_audit_logs_timestamp ON audit_logs(timestamp);

-- Prevent UPDATE/DELETE on audit_logs (immutability)
CREATE OR REPLACE FUNCTION prevent_audit_log_mutation() RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Audit logs are immutable';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER audit_log_immutability
BEFORE UPDATE OR DELETE ON audit_logs
FOR EACH ROW EXECUTE FUNCTION prevent_audit_log_mutation();

-- EnvironmentSecret table
CREATE TABLE environment_secrets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    key VARCHAR(200) NOT NULL UNIQUE,
    encrypted_value TEXT NOT NULL,
    key_vault_secret_name VARCHAR(200) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),
    last_accessed_at TIMESTAMP
);

CREATE INDEX idx_environment_secrets_key ON environment_secrets(key);
CREATE INDEX idx_environment_secrets_last_accessed ON environment_secrets(last_accessed_at);
```

**Push Migration to Supabase**:
```powershell
supabase db push
```

**Verify Tables**:
```powershell
supabase db list-tables
# Should output: campaigns, leads, tasks, outreach_messages, audit_logs, environment_secrets
```

---

### Step 4: Create Solution and Projects

**Create Directory.Build.props** (root level):
```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisMode>All</AnalysisMode>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

**Create Directory.Packages.props** (root level):
```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- .NET MAUI -->
    <PackageVersion Include="Microsoft.Maui.Controls" Version="10.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Components.WebView.Maui" Version="10.0.0" />
    
    <!-- Supabase (renamed from supabase-csharp in 2024) -->
    <PackageVersion Include="Supabase" Version="1.0.0" />
    <PackageVersion Include="Supabase.Postgrest" Version="4.1.0" />
    
    <!-- Azure SDKs -->
    <PackageVersion Include="Azure.Identity" Version="1.13.0" />
    <PackageVersion Include="Azure.Security.KeyVault.Secrets" Version="4.8.0" />
    <PackageVersion Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.4.0" />
    
    <!-- OpenTelemetry -->
    <PackageVersion Include="OpenTelemetry" Version="1.10.0" />
    <PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.10.0" />
    
    <!-- Playwright -->
    <PackageVersion Include="Microsoft.Playwright" Version="1.57.0" />
    
    <!-- Code Analysis -->
    <PackageVersion Include="StyleCop.Analyzers" Version="1.2.0-beta.556" />
    <PackageVersion Include="SonarAnalyzer.CSharp" Version="10.5.0.104116" />
    
    <!-- Testing -->
    <PackageVersion Include="xUnit" Version="2.9.3" />
    <PackageVersion Include="xUnit.runner.visualstudio" Version="2.9.3" />
    <PackageVersion Include="Moq" Version="4.20.72" />
  </ItemGroup>
</Project>
```

**Create .editorconfig** (root level):
```ini
root = true

[*]
charset = utf-8
indent_style = space
indent_size = 4
insert_final_newline = true
trim_trailing_whitespace = true

[*.{cs,csx,vb,vbx}]
# StyleCop: Enforce XML documentation
dotnet_diagnostic.SA1600.severity = warning
dotnet_diagnostic.SA1633.severity = none  # File header not required

# SonarAnalyzer: Cognitive complexity
dotnet_diagnostic.S3776.severity = warning
```

**Create Solution and Projects**:
```powershell
cd C:\src\outreach-agent\src

# Create solution
dotnet new sln --name OutreachAgent

# Create projects
dotnet new classlib --name OutreachAgent.Core --framework net10.0
dotnet new classlib --name OutreachAgent.Agent --framework net10.0
dotnet new classlib --name OutreachAgent.MCP --framework net10.0
dotnet new maui-blazor --name OutreachAgent.UI --framework net10.0
dotnet new xunit --name OutreachAgent.Tests --framework net10.0

# Add projects to solution
dotnet sln add OutreachAgent.Core\OutreachAgent.Core.csproj
dotnet sln add OutreachAgent.Agent\OutreachAgent.Agent.csproj
dotnet sln add OutreachAgent.MCP\OutreachAgent.MCP.csproj
dotnet sln add OutreachAgent.UI\OutreachAgent.UI.csproj
dotnet sln add OutreachAgent.Tests\OutreachAgent.Tests.csproj

# Add project references
cd OutreachAgent.UI
dotnet add reference ..\OutreachAgent.Core\OutreachAgent.Core.csproj
dotnet add reference ..\OutreachAgent.Agent\OutreachAgent.Agent.csproj
dotnet add reference ..\OutreachAgent.MCP\OutreachAgent.MCP.csproj

cd ..\OutreachAgent.Tests
dotnet add reference ..\OutreachAgent.Core\OutreachAgent.Core.csproj
```

---

### Step 5: Configure Application Settings

**Create `appsettings.Development.json`** in `OutreachAgent.UI/`:
```json
{
  "Supabase": {
    "Url": "{KeyVault:supabase-url}",
    "AnonKey": "{KeyVault:supabase-anon-key}",
    "DatabaseUrl": "{KeyVault:supabase-database-url}"
  },
  "Azure": {
    "KeyVault": {
      "VaultUri": "https://<your-key-vault-name>.vault.azure.net/"
    },
    "OpenAI": {
      "Endpoint": "{KeyVault:openai-endpoint}",
      "ApiKey": "{KeyVault:openai-api-key}",
      "DeploymentName": "gpt-4-turbo"
    }
  },
  "ApplicationInsights": {
    "ConnectionString": "<your-connection-string>"
  },
  "MCP": {
    "Servers": {
      "playwright": {
        "Executable": "mcp-servers/playwright/playwright-mcp.exe",
        "Args": [],
        "ExpectedHash": "PLACEHOLDER",
        "AllowedTools": ["linkedin_scrape_profile", "linkedin_send_message"]
      },
      "desktop-commander": {
        "Executable": "mcp-servers/desktop-commander/desktop-commander.exe",
        "Args": ["--sandbox"],
        "ExpectedHash": "PLACEHOLDER",
        "AllowedTools": ["export_campaign_data", "import_leads_from_csv"]
      }
    }
  }
}
```

**Update `MauiProgram.cs`**:
```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Monitor.OpenTelemetry.AspNetCore;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Add configuration
        builder.Configuration.AddJsonFile("appsettings.json", optional: false);
        builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true);

        // Add OpenTelemetry + Application Insights
        builder.Services.AddOpenTelemetry()
            .UseAzureMonitor(options =>
            {
                options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
            });

        // Add logging
        builder.Services.AddLogging(logging =>
        {
            logging.AddDebug();
            logging.AddFilter("Microsoft.AspNetCore.Components.WebView", LogLevel.Trace);
        });

        return builder.Build();
    }
}
```

---

### Step 6: Run Application

**Build Solution**:
```powershell
cd C:\src\outreach-agent\src
dotnet build
```

**Run on Windows**:
```powershell
cd OutreachAgent.UI
dotnet run --framework net10.0-windows10.0.19041.0
```

**Expected Output**:
- MAUI window opens with Blazor UI
- Application Insights telemetry starts flowing to Azure
- No errors in Debug Output

---

## Verification Checklist

- [ ] .NET 10 SDK installed (`dotnet --version`)
- [ ] Visual Studio 2022 with MAUI workload
- [ ] Azure resources created (Key Vault, App Insights, OpenAI)
- [ ] Supabase project provisioned
- [ ] Database schema applied (`supabase db list-tables`)
- [ ] Solution builds without errors (`dotnet build`)
- [ ] Application runs on Windows (`dotnet run`)
- [ ] Application Insights shows telemetry in Azure Portal

---

## Troubleshooting

### Error: "Workload 'maui-windows' not installed"
**Solution**:
```powershell
dotnet workload install maui-windows
```

### Error: "Azure Key Vault access denied"
**Solution**: Verify RBAC role assignment:
```powershell
az role assignment list --assignee $(az ad signed-in-user show --query id -o tsv) --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.KeyVault/vaults/<key-vault-name>
```

### Error: "Supabase migration failed"
**Solution**: Check Supabase dashboard → Database → SQL Editor → Run migration manually

### Error: "Playwright executable not found"
**Solution**: Download MCP servers (will be added in Phase 2 implementation)

---

## Next Steps

1. **Phase 2 Implementation**: Build Controller, Agent, MCP integration (per `tasks.md`)
2. **Test LinkedIn Authentication**: Manually obtain session cookie, store in Key Vault
3. **Run First Campaign**: Create test campaign with 5 leads, execute scraping tasks

**Estimated Setup Time**: 45-60 minutes (including Azure provisioning)
