// <copyright file="Program.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text;
using Atlassian.Mcp.Server.Common;
using Atlassian.Mcp.Server.Common.Http;
using Atlassian.Mcp.Server.Configuration;
using Atlassian.Mcp.Server.Confluence;
using Atlassian.Mcp.Server.Jira;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

UseUtf8Console();

AtlassianOptions? options = AtlassianOptions.FromEnvironment(Environment.GetEnvironmentVariable, out IReadOnlyList<string> errors);
if (options is null)
{
    // Standard output carries the MCP protocol, so configuration errors go to standard error,
    // which MCP clients show in their server logs.
    foreach (string error in errors)
    {
        await Console.Error.WriteLineAsync(error);
    }

    await Console.Error.WriteLineAsync("See https://github.com/icnocop/Atlassian-MCP-Server#configuration.");
    return 1;
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<JiraMetadataCache>();
builder.Services.AddHttpClient<AtlassianHttpClient>(http => AtlassianHttpClient.Configure(http, options));
builder.Services.AddTransient<JiraClient>();
builder.Services.AddTransient<ConfluenceClient>();

builder.Services
    .AddMcpServer(server => server.ServerInfo = new Implementation { Name = ServerInfo.Name, Version = ServerInfo.Version })
    .WithStdioServerTransport()
    .WithAtlassianTools(options);

await builder.Build().RunAsync();
return 0;

// The MCP transport exchanges UTF-8 JSON over standard input and output. On Windows the console
// defaults to the OEM code page, which corrupts multi-byte characters (an em dash arrives as
// "ΓÇö"), so both streams are switched to UTF-8 before the host starts.
static void UseUtf8Console()
{
    try
    {
        Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
    catch (IOException)
    {
        // Some hosts do not allow redirected streams to be reconfigured; keep the default encoding.
    }
}
