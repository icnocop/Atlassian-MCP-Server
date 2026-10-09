// <copyright file="ElicitationUploadApprovalTests.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Text.Json;
using Atlassian.Mcp.Server.Common.Attachments;
using ModelContextProtocol.Protocol;

namespace Atlassian.Mcp.Server.Tests.Common.Attachments;

/// <summary>
/// Tests for <see cref="ElicitationUploadApproval"/>.
/// </summary>
[TestClass]
public sealed class ElicitationUploadApprovalTests
{
    /// <summary>
    /// Verifies that the question names the file, its size, the destination, and offers the three choices.
    /// </summary>
    [TestMethod]
    public void CreateRequest_WithFile_ShowsTheFileAndTheThreeChoices()
    {
        // Arrange
        var request = new UploadApprovalRequest(@"C:\Shots\1.png", 112_082, @"C:\Shots", "Jira issue PROJ-1");

        // Act
        ElicitRequestParams elicitation = ElicitationUploadApproval.CreateRequest(request);

        // Assert
        StringAssert.Contains(elicitation.Message, "Jira issue PROJ-1", StringComparison.Ordinal);
        StringAssert.Contains(elicitation.Message, @"C:\Shots\1.png", StringComparison.Ordinal);
        StringAssert.Contains(elicitation.Message, "110 KB", StringComparison.Ordinal);

        Assert.IsNotNull(elicitation.RequestedSchema);
        var decision = (ElicitRequestParams.TitledSingleSelectEnumSchema)elicitation.RequestedSchema.Properties[ElicitationUploadApproval.DecisionField];
        CollectionAssert.AreEqual(
            new[] { ElicitationUploadApproval.Once, ElicitationUploadApproval.Always, ElicitationUploadApproval.Deny },
            decision.OneOf.Select(option => option.Const).ToArray());
        StringAssert.Contains(decision.OneOf[1].Title, @"C:\Shots", StringComparison.Ordinal);
        CollectionAssert.AreEqual(new[] { ElicitationUploadApproval.DecisionField }, elicitation.RequestedSchema.Required!.ToArray());
    }

    /// <summary>
    /// Verifies that each accepted choice maps to its decision.
    /// </summary>
    /// <param name="choice">The value the user chose.</param>
    /// <param name="expected">The expected decision.</param>
    [TestMethod]
    [DataRow(ElicitationUploadApproval.Once, UploadDecision.AllowOnce)]
    [DataRow(ElicitationUploadApproval.Always, UploadDecision.AlwaysAllowFolder)]
    [DataRow(ElicitationUploadApproval.Deny, UploadDecision.Denied)]
    [DataRow("something else", UploadDecision.Denied)]
    public void ParseDecision_WithAcceptedChoice_ReturnsTheDecision(string choice, UploadDecision expected)
    {
        // Arrange
        var result = new ElicitResult
        {
            Action = "accept",
            Content = new Dictionary<string, JsonElement> { [ElicitationUploadApproval.DecisionField] = JsonSerializer.SerializeToElement(choice) },
        };

        // Act
        UploadDecision decision = ElicitationUploadApproval.ParseDecision(result);

        // Assert
        Assert.AreEqual(expected, decision);
    }

    /// <summary>
    /// Verifies that a declined or canceled question denies the upload, whatever the content says.
    /// </summary>
    /// <param name="action">The action the user took.</param>
    [TestMethod]
    [DataRow("decline")]
    [DataRow("cancel")]
    public void ParseDecision_WhenNotAccepted_ReturnsDenied(string action)
    {
        // Arrange
        var result = new ElicitResult
        {
            Action = action,
            Content = new Dictionary<string, JsonElement> { [ElicitationUploadApproval.DecisionField] = JsonSerializer.SerializeToElement(ElicitationUploadApproval.Once) },
        };

        // Act
        UploadDecision decision = ElicitationUploadApproval.ParseDecision(result);

        // Assert
        Assert.AreEqual(UploadDecision.Denied, decision);
    }
}
