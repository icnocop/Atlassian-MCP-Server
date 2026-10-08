// <copyright file="QueryString.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

using System.Globalization;
using System.Text;

namespace Atlassian.Mcp.Server.Common.Http;

/// <summary>
/// Builds a request path with an escaped query string, skipping parameters that have no value.
/// </summary>
public sealed class QueryString
{
    private readonly string path;
    private readonly List<KeyValuePair<string, string>> parameters = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryString"/> class.
    /// </summary>
    /// <param name="path">The path, without a query string.</param>
    public QueryString(string path)
    {
        this.path = path;
    }

    /// <summary>
    /// Adds a parameter when <paramref name="value"/> is not <see langword="null"/> or empty.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <returns>This instance.</returns>
    public QueryString Add(string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            this.parameters.Add(new KeyValuePair<string, string>(name, value));
        }

        return this;
    }

    /// <summary>
    /// Adds a parameter when <paramref name="value"/> has a value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <returns>This instance.</returns>
    public QueryString Add(string name, int? value)
        => this.Add(name, value?.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Adds a parameter when <paramref name="value"/> has a value.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <returns>This instance.</returns>
    public QueryString Add(string name, long? value)
        => this.Add(name, value?.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Adds a parameter when <paramref name="value"/> has a value, as <c>true</c> or <c>false</c>.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The value.</param>
    /// <returns>This instance.</returns>
    public QueryString Add(string name, bool? value)
        => this.Add(name, value is null ? null : (value.Value ? "true" : "false"));

    /// <summary>
    /// Adds the parameter once for each value, as the Atlassian APIs expect for multi-valued parameters.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="values">The values.</param>
    /// <returns>This instance.</returns>
    public QueryString AddEach(string name, IEnumerable<string>? values)
    {
        foreach (string value in values ?? [])
        {
            this.Add(name, value);
        }

        return this;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (this.parameters.Count == 0)
        {
            return this.path;
        }

        var builder = new StringBuilder(this.path);
        char separator = this.path.Contains('?', StringComparison.Ordinal) ? '&' : '?';

        foreach ((string name, string value) in this.parameters)
        {
            builder.Append(separator).Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(value));
            separator = '&';
        }

        return builder.ToString();
    }
}
