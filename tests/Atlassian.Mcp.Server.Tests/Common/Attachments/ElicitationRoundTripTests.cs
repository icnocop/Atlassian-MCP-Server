// <copyright file="ElicitationRoundTripTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.IO.Pipelines;
using System.Text.Json;
using Atlassian.Mcp.Server.Common.Attachments;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Tests.Common.Attachments;

/// <summary>
/// Tests <see cref="ElicitationUploadApproval"/> against a real MCP client, connected in memory, so that the
/// capability check and the request and answer are exercised as a client sees them.
/// </summary>
[TestClass]
public sealed class ElicitationRoundTripTests
{
    private const string ToolName = "ask";

    /// <summary>
    /// Verifies that a client that can show forms is asked, and that its answer comes back as the decision.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task RequestAsync_WithClientThatAnswers_ReturnsTheAnswer()
    {
        // Arrange
        ElicitRequestParams? asked = null;
        var handlers = new McpClientHandlers
        {
            ElicitationHandler = (request, _) =>
            {
                asked = request;
                return ValueTask.FromResult(new ElicitResult
                {
                    Action = "accept",
                    Content = new Dictionary<string, JsonElement>
                    {
                        [ElicitationUploadApproval.DecisionField] = JsonSerializer.SerializeToElement(ElicitationUploadApproval.Always),
                    },
                });
            },
        };

        // Act
        string decision = await CallAsync(handlers);

        // Assert
        Assert.AreEqual(nameof(UploadDecision.AlwaysAllowFolder), decision);
        StringAssert.Contains(asked?.Message, @"C:\Shots\1.png", StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a client without elicitation is not asked, so the tool call fails fast instead of waiting.
    /// </summary>
    /// <returns>A task.</returns>
    [TestMethod]
    public async Task RequestAsync_WithClientWithoutElicitation_ReturnsUnavailable()
    {
        // Act
        string decision = await CallAsync(handlers: null);

        // Assert
        Assert.AreEqual(nameof(UploadDecision.Unavailable), decision);
    }

    private static async Task<string> CallAsync(McpClientHandlers? handlers)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        McpServerTool tool = McpServerTool.Create(
            async (McpServer server, CancellationToken cancellationToken) =>
            {
                UploadDecision decision = await new ElicitationUploadApproval(server).RequestAsync(
                    new UploadApprovalRequest(@"C:\Shots\1.png", 2048, @"C:\Shots", "Jira issue PROJ-1"),
                    cancellationToken);
                return decision.ToString();
            },
            new McpServerToolCreateOptions { Name = ToolName });

        await using McpServer mcpServer = McpServer.Create(
            new StreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream()),
            new McpServerOptions { ToolCollection = [tool] });
        Task running = mcpServer.RunAsync();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using (McpClient client = await McpClient.CreateAsync(
            new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream()),
            new McpClientOptions { Handlers = handlers ?? new McpClientHandlers() },
            cancellationToken: timeout.Token))
        {
            CallToolResult result = await client.CallToolAsync(ToolName, cancellationToken: timeout.Token);
            return ((TextContentBlock)result.Content.Single()).Text;
        }
    }
}
