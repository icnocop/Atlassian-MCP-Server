# Uploading files by path

The attachment tools accept a file as base64 or as the path of a local file. Use a path for anything already on disk, such as a screenshot or a log: base64 makes the model copy hundreds of kilobytes of text character for character, and one wrong character uploads a corrupted file.

A path lets the model send a file from your computer to your site, so the server decides which files it may read, and asks you rather than the model:

- A file in an allowed folder is uploaded without asking. The allowed folders are those in `ATLASSIAN_ATTACHMENT_FOLDERS`, plus those you chose to always allow, which the server saves in `%APPDATA%\Atlassian.Mcp.Server\attachment-folders.json` (`~/.config/Atlassian.Mcp.Server/attachment-folders.json` elsewhere). Remove a folder by editing that file; the change applies immediately, in every client and session.
- For any other file, the server asks you, through your MCP client, whether to upload it, showing the full path, its size, and where it goes: **Allow once**, **Always allow** its folder, or **Deny**. This uses MCP [elicitation](https://modelcontextprotocol.io/specification/draft/client/elicitation), which Claude Code and GitHub Copilot in VS Code support. A question not answered within two minutes counts as Deny.
- In a client that cannot ask, such as Claude Desktop, a file outside the allowed folders is refused.
- Relative paths, files over 100 MB, and files reached through a symbolic link or junction are always refused. Folders are compared as written: a Windows 8.3 short path such as `C:\Users\FIRSTN~1\…` does not match the same folder written in full.

List or always allow only folders whose files you are willing to share, such as a screenshots folder. `atlassian_jira_get_configuration` shows both lists.
