// =======================
// AgentRuntime.cs
// =======================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Maui.Storage;

#region ===== DOMAIN =====

enum CampaignStatus { Initializing, Active, Paused, Completed, Error }
enum TaskStatus { Pending, InProgress, Done }
enum AgentCommandType { UserMessage, Stop, Resume }

record Campaign(Guid Id, string Name, CampaignStatus Status);
record CampaignTask(Guid Id, string Description, TaskStatus Status);
record Artifact(string Type, string Key, string Content);

record CampaignState(
    Campaign Campaign,
    List<CampaignTask> Tasks,
    List<Artifact> Artifacts
);

record AgentCommand(AgentCommandType Type, string? Payload = null);

#endregion

#region ===== CHAT OUTPUT (UI ONLY) =====

record ChatMessage(DateTime Timestamp, string Role, string Content);

#endregion

#region ===== AGENT PROPOSAL =====

record AgentProposal(
    string Type,          // fetch | request_user_input | no_op
    string? Argument,
    string? Reason
);

#endregion

#region ===== CAMPAIGN RUNNER (ENTRY POINT) =====

public sealed class CampaignRunner
{
    private readonly Channel<AgentCommand> _commands =
        Channel.CreateUnbounded<AgentCommand>();

    private readonly AgentController _controller;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public event Action<ChatMessage>? OnChat;

    public CampaignRunner()
    {
        _controller = new AgentController(EmitChat);
    }

    public void Start()
    {
        if (_loop != null) return;
        _cts = new CancellationTokenSource();
        _loop = Task.Run(() => LoopAsync(_cts.Token));
    }

    public async Task SendUserMessageAsync(string message)
    {
        EmitChat("user", message);
        await _commands.Writer.WriteAsync(
            new AgentCommand(AgentCommandType.UserMessage, message)
        );
    }

    public async Task StopAsync()
        => await _commands.Writer.WriteAsync(new AgentCommand(AgentCommandType.Stop));

    public async Task ResumeAsync()
        => await _commands.Writer.WriteAsync(new AgentCommand(AgentCommandType.Resume));

    private async Task LoopAsync(CancellationToken token)
    {
        Guid? campaignId = null;
        bool paused = false;

        EmitChat("system", "Agent runtime started.");

        while (!token.IsCancellationRequested)
        {
            var cmd = await _commands.Reader.ReadAsync(token);

            switch (cmd.Type)
            {
                case AgentCommandType.UserMessage:
                    campaignId = await _controller.HandleUserMessageAsync(
                        campaignId, cmd.Payload!, token);
                    paused = false;
                    break;

                case AgentCommandType.Stop:
                    paused = true;
                    if (campaignId != null)
                        await _controller.PauseAsync(campaignId.Value);
                    EmitChat("system", "Campaign paused.");
                    break;

                case AgentCommandType.Resume:
                    paused = false;
                    EmitChat("system", "Campaign resumed.");
                    break;
            }

            while (!paused && !token.IsCancellationRequested)
            {
                await _controller.ExecuteOneStepAsync(campaignId!.Value, token);

                if (_commands.Reader.TryPeek(out _))
                    break;

                await Task.Delay(50, token);
            }
        }
    }

    private void EmitChat(string role, string content)
        => OnChat?.Invoke(new ChatMessage(DateTime.UtcNow, role, content));
}

#endregion

#region ===== CONTROLLER =====

sealed class AgentController
{
    private readonly SupabaseRepo _db;
    private readonly OpenAiClient _llm;
    private readonly Action<string, string> _chat;

    public AgentController(Action<string, string> chat)
    {
        _db = new SupabaseRepo();
        _llm = new OpenAiClient();
        _chat = chat;
    }

    public async Task<Guid> HandleUserMessageAsync(
        Guid? existingCampaign,
        string message,
        CancellationToken token)
    {
        var id = existingCampaign ?? await CreateCampaignAsync(message);
        await _db.InsertArtifactAsync(id, new Artifact("user_message", Guid.NewGuid().ToString(), message));
        await _db.InsertTaskAsync(id, new CampaignTask(Guid.NewGuid(), $"Process: {message}", TaskStatus.Pending));
        _chat("system", "User instruction recorded.");
        return id;
    }

    public async Task ExecuteOneStepAsync(Guid campaignId, CancellationToken token)
    {
        var state = await _db.LoadStateAsync(campaignId);
        var task = state.Tasks.FirstOrDefault(t => t.Status == TaskStatus.Pending);

        if (task == null)
        {
            await _db.MarkCompletedAsync(campaignId);
            _chat("system", "Campaign completed.");
            return;
        }

        await _db.MarkTaskInProgressAsync(task.Id);
        _chat("system", $"Executing: {task.Description}");

        var (proposal, explanation) =
            await _llm.ProposeAsync(task.Description, state.Artifacts, token);

        if (explanation != null)
            _chat("agent", explanation);

        if (proposal.Type == "request_user_input")
        {
            await _db.MarkPausedAsync(campaignId);
            _chat("agent", proposal.Argument!);
            _chat("system", "Waiting for user input.");
            return;
        }

        if (proposal.Type == "fetch")
        {
            var content = await HttpTool.FetchAsync(proposal.Argument!, token);
            await _db.InsertArtifactAsync(campaignId,
                new Artifact("research", proposal.Argument!, content));
        }

        await _db.MarkTaskDoneAsync(task.Id);
        _chat("system", "Task completed.");
    }

    public Task PauseAsync(Guid id) => _db.MarkPausedAsync(id);

    private async Task<Guid> CreateCampaignAsync(string message)
    {
        var name = $"Campaign-{DateTime.UtcNow:yyyyMMddHHmm}";
        var id = Guid.NewGuid();
        await _db.InsertCampaignAsync(new Campaign(id, name, CampaignStatus.Active));
        _chat("system", $"Campaign created: {name}");
        return id;
    }
}

#endregion

#region ===== OPENAI (REAL SDK) =====

sealed class OpenAiClient
{
    private readonly OpenAIClient _client;

    public OpenAiClient()
    {
        var endpoint = SecureStorage.GetAsync("AZURE_OPENAI_ENDPOINT").Result!;
        var key = SecureStorage.GetAsync("AZURE_OPENAI_KEY").Result!;
        _client = new OpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
    }

    public async Task<(AgentProposal, string?)> ProposeAsync(
        string task,
        List<Artifact> artifacts,
        CancellationToken token)
    {
        var payload = JsonSerializer.Serialize(new { task, artifacts });

        var response = await _client.GetChatCompletionsAsync(
            "gpt-4o-mini",
            new ChatCompletionsOptions
            {
                Temperature = 0,
                Messages =
                {
                    new ChatMessage(ChatRole.System,
                        "Return JSON only. Allowed actions: fetch, request_user_input."),
                    new ChatMessage(ChatRole.User, payload)
                }
            },
            token
        );

        var json = response.Value.Choices[0].Message.Content;
        var doc = JsonDocument.Parse(json!).RootElement;

        return (
            new AgentProposal(
                doc.GetProperty("action").GetString()!,
                doc.GetProperty("argument").GetString(),
                doc.TryGetProperty("reason", out var r) ? r.GetString() : null
            ),
            doc.TryGetProperty("explanation", out var e) ? e.GetString() : null
        );
    }
}

#endregion

#region ===== SUPABASE (POSTGRES) =====

sealed class SupabaseRepo
{
    private readonly string _cs;

    public SupabaseRepo()
    {
        _cs = SecureStorage.GetAsync("SUPABASE_CONN").Result!;
    }

    NpgsqlConnection Conn() => new NpgsqlConnection(_cs);

    public async Task InsertCampaignAsync(Campaign c)
    {
        using var db = Conn();
        await db.ExecuteAsync(
            "insert into campaigns(id,name,status) values(@Id,@Name,@Status)", c);
    }

    public async Task InsertTaskAsync(Guid cid, CampaignTask t)
    {
        using var db = Conn();
        await db.ExecuteAsync(
            "insert into tasks(id,campaign_id,description,status) values(@Id,@cid,@Description,@Status)",
            new { t.Id, cid, t.Description, t.Status });
    }

    public async Task InsertArtifactAsync(Guid cid, Artifact a)
    {
        using var db = Conn();
        await db.ExecuteAsync(
            "insert into artifacts(campaign_id,type,key,content) values(@cid,@Type,@Key,@Content)",
            new { cid, a.Type, a.Key, a.Content });
    }

    public async Task<CampaignState> LoadStateAsync(Guid id)
    {
        using var db = Conn();
        var campaign = await db.QuerySingleAsync<Campaign>(
            "select * from campaigns where id=@id", new { id });
        var tasks = (await db.QueryAsync<CampaignTask>(
            "select * from tasks where campaign_id=@id", new { id })).ToList();
        var artifacts = (await db.QueryAsync<Artifact>(
            "select type,key,content from artifacts where campaign_id=@id", new { id })).ToList();

        return new CampaignState(campaign, tasks, artifacts);
    }

    public Task MarkTaskInProgressAsync(Guid id)
        => Exec("update tasks set status='InProgress' where id=@id", id);

    public Task MarkTaskDoneAsync(Guid id)
        => Exec("update tasks set status='Done' where id=@id", id);

    public Task MarkPausedAsync(Guid id)
        => Exec("update campaigns set status='Paused' where id=@id", id);

    public Task MarkCompletedAsync(Guid id)
        => Exec("update campaigns set status='Completed' where id=@id", id);

    private async Task Exec(string sql, Guid id)
    {
        using var db = Conn();
        await db.ExecuteAsync(sql, new { id });
    }
}

#endregion

#region ===== TOOLS =====

static class HttpTool
{
    public static async Task<string> FetchAsync(string url, CancellationToken token)
    {
        using var http = new System.Net.Http.HttpClient();
        return await http.GetStringAsync(url, token);
    }
}

#endregion
