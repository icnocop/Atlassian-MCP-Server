# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Jira tools for issues, search, comments, links, attachments, projects, users, worklogs, filters, and agile boards.
- Confluence tools for pages, private drafts, section editing, search, spaces, comments, attachments, and labels.
- Markdown to Atlassian Document Format conversion for writing, and back to Markdown for reading.
- Toolsets, an allow list of tools, and a read-only mode.
- `atlassian_jira_add_attachment` and `atlassian_confluence_add_attachment` upload a local file by `filePath`, so that a file on disk no longer has to pass through the model as base64. Files in the folders that `ATLASSIAN_ATTACHMENT_FOLDERS` lists are uploaded directly; for any other file the server asks the user, through MCP elicitation, to allow it once, always allow its folder, or deny it.
