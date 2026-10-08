// <copyright file="FixedTimeProvider.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Tests.Jira.Projects;

/// <summary>
/// A <see cref="TimeProvider"/> that always returns the same time.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset now;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedTimeProvider"/> class.
    /// </summary>
    /// <param name="now">The time to return.</param>
    public FixedTimeProvider(DateTimeOffset now)
    {
        this.now = now;
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => this.now;
}
