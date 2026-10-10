// <copyright file="UserCandidate.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Common.Adf;

/// <summary>
/// A user that a mention name might refer to.
/// </summary>
/// <param name="AccountId">The Atlassian account ID.</param>
/// <param name="DisplayName">The display name.</param>
public sealed record UserCandidate(string AccountId, string DisplayName);
