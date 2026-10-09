// <copyright file="ElicitationUploadApproval.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Atlassian.Mcp.Server.Common.Attachments;

/// <summary>
/// Asks the user through MCP elicitation: the client shows the question in its own dialog, so the
/// answer comes from the user and never passes through the model.
/// </summary>
public sealed class ElicitationUploadApproval : IUploadApproval
{
    /// <summary>The name of the form field that holds the decision.</summary>
    internal const string DecisionField = "decision";

    /// <summary>The value of <see cref="DecisionField"/> that allows one upload.</summary>
    internal const string Once = "once";

    /// <summary>The value of <see cref="DecisionField"/> that allows the folder from now on.</summary>
    internal const string Always = "always";

    /// <summary>The value of <see cref="DecisionField"/> that refuses the upload.</summary>
    internal const string Deny = "deny";

    /// <summary>
    /// How long to wait for an answer. A client running without a user, such as <c>claude -p</c>, can
    /// leave the question open indefinitely, which would hang the tool call.
    /// </summary>
    internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    private readonly McpServer server;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElicitationUploadApproval"/> class.
    /// </summary>
    /// <param name="server">The MCP server handling the current tool call.</param>
    public ElicitationUploadApproval(McpServer server)
    {
        this.server = server;
    }

    /// <inheritdoc/>
    public async Task<UploadDecision> RequestAsync(UploadApprovalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ElicitationCapability? elicitation = this.server.ClientCapabilities?.Elicitation;

        // An empty elicitation capability means form mode; a client that declares only URL mode cannot show a form.
        if (elicitation is null || (elicitation.Form is null && elicitation.Url is not null))
        {
            return UploadDecision.Unavailable;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        ElicitResult result;
        try
        {
            result = await this.server.ElicitAsync(CreateRequest(request), timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UploadDecision.Denied;
        }

        return ParseDecision(result);
    }

    /// <summary>
    /// Creates the question shown to the user.
    /// </summary>
    /// <param name="request">What would be uploaded, and where to.</param>
    /// <returns>The elicitation request.</returns>
    internal static ElicitRequestParams CreateRequest(UploadApprovalRequest request)
        => new()
        {
            Message =
                $"Upload this file to {request.Destination}?\n\n{request.FilePath}\n{FormatSize(request.Length)}\n\n" +
                "It is not in a folder this server may upload from. Check that it is the file you expect: once uploaded, anyone who can see the item can read it.",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                {
                    [DecisionField] = new ElicitRequestParams.TitledSingleSelectEnumSchema
                    {
                        Title = "Upload",
                        OneOf =
                        [
                            new ElicitRequestParams.EnumSchemaOption { Const = Once, Title = "Allow once" },
                            new ElicitRequestParams.EnumSchemaOption { Const = Always, Title = $"Always allow files in {request.Folder}" },
                            new ElicitRequestParams.EnumSchemaOption { Const = Deny, Title = "Deny" },
                        ],
                    },
                },
                Required = [DecisionField],
            },
        };

    /// <summary>
    /// Reads the user's decision from the answer.
    /// </summary>
    /// <param name="result">The answer.</param>
    /// <returns>The decision. Anything but an accepted answer that allows the upload is <see cref="UploadDecision.Denied"/>.</returns>
    internal static UploadDecision ParseDecision(ElicitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsAccepted
            || result.Content is null
            || !result.Content.TryGetValue(DecisionField, out JsonElement value)
            || value.ValueKind != JsonValueKind.String)
        {
            return UploadDecision.Denied;
        }

        return value.GetString() switch
        {
            Once => UploadDecision.AllowOnce,
            Always => UploadDecision.AlwaysAllowFolder,
            _ => UploadDecision.Denied,
        };
    }

    private static string FormatSize(long bytes)
        => bytes < 1024 * 1024
            ? string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, (bytes + 1023) / 1024):N0} KB")
            : string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024.0):N1} MB");
}
