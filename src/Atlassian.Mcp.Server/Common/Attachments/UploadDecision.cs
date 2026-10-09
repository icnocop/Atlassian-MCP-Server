// <copyright file="UploadDecision.cs" company="icnocop">
// Copyright (c) icnocop. Licensed under the MIT License.
// </copyright>

namespace Atlassian.Mcp.Server.Common.Attachments;

/// <summary>
/// The user's answer to whether a file outside the allowed folders may be uploaded.
/// </summary>
public enum UploadDecision
{
    /// <summary>The user could not be asked, because the client cannot show a question.</summary>
    Unavailable,

    /// <summary>The user refused, dismissed the question, or did not answer in time.</summary>
    Denied,

    /// <summary>The user allowed this one upload.</summary>
    AllowOnce,

    /// <summary>The user allowed this upload and every later upload from the file's folder.</summary>
    AlwaysAllowFolder,
}
