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
- User mentions in Markdown for Jira and Confluence. `@[Display Name]` is resolved to an account ID with a user search, and `@[Display Name](accountid:ID)` gives the account ID directly; either becomes a mention that notifies the user. A name that matches no user, or more than one, fails the call before anything is written, listing the matching users and their account IDs. Mentions are read back as `@[Display Name](accountid:ID)`, so they survive a read, edit, and write, and a page that contains mentions can now be edited with Markdown.
- Embedded files in Jira rich text. `![name](attachment:ID)`, in a paragraph of its own, embeds a file already attached to the issue in a comment, description, environment, worklog comment, or multi-line custom field: images and videos inline, other files as file cards, as Jira's editor shows them. A missing attachment fails the call before anything is written. `atlassian_jira_get_issue`, `atlassian_jira_get_comments`, and `atlassian_jira_get_comment` read embedded files back the same way, so they survive a read, edit, and write; a file Jira cannot render stays a placeholder. Confluence refuses the syntax. For this, `atlassian_jira_get_comments` and `atlassian_jira_get_comment` gain a `richTextFormat` parameter and now return Markdown by default, like `atlassian_jira_get_issue`; pass `adf` for the ADF they returned before.
- `atlassian_confluence_delete_attachment` moves a Confluence attachment to the space's trash, from where a space administrator can restore it; it never deletes one permanently. Like page deletion, it is in the opt-in `confluence-admin` toolset.
- Every tool call is checked against the tool's parameters before it runs. A missing required argument, or an argument with a name the tool does not have (such as `content` instead of `body`), fails the call with a message that names the problem and lists the tool's parameters, instead of the generic "An error occurred invoking" or silently ignoring the argument.
- A `restrictions` parameter on `atlassian_confluence_create_page`, `atlassian_confluence_update_page`, and `atlassian_confluence_publish_page` sets who can view and edit the page: `open` (anyone who can see the space can view and edit it), `edit-restricted` (anyone can view it; only you can edit it), or `private` (only you can view and edit it). A page created as a private draft stays private when it is published, so pass `restrictions: open` to `atlassian_confluence_publish_page` to let the space see it. On `atlassian_confluence_create_page` it replaces `restrictToMe`, with the same defaults: private for a draft and open for a published page. On the other two tools it is optional, and leaving it out leaves the page's restrictions unchanged.

### Fixed

- `atlassian_confluence_move_page` with `append` refuses to move a page to the parent it already has, instead of reporting success. Confluence leaves such a page where it is, so reordering child pages that way appeared to work but did nothing; the error says to use `before` or `after` a sibling page.
