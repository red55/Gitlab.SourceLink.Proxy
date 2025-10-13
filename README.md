# Gitlab.SourceLink.Proxy

## What is this?

**Gitlab.SourceLink.Proxy** is a reverse proxy designed to enable [SourceLink](https://github.com/dotnet/sourcelink) support for private GitLab repositories.  
GitLab does not natively support SourceLink for private repositories, which makes it difficult for .NET developers to enable source debugging and symbol resolution in private projects. This proxy bridges that gap.

## What does it do?

- **URL Rewriting:**  
  The proxy intercepts SourceLink requests that use the legacy "raw" file URL format and rewrites them to the appropriate GitLab API endpoint for file download. This ensures that SourceLink can retrieve source files from private repositories in a way that GitLab understands and authorizes.

- **Authentication Handling:**  
  GitLab's API returns a `404 Not Found` for unauthenticated requests to private files, which does not prompt IDEs or debuggers to ask for credentials.  
  This proxy detects such cases and instead returns a `401 Unauthorized` response with a `WWW-Authenticate: Basic realm="Gitlab"` header. This prompts tools (like Visual Studio) to request credentials from the user, enabling proper authentication flow.

- **SourceLink User-Agent Detection:**  
  The proxy only processes requests that originate from SourceLink clients, avoiding unnecessary processing for unrelated traffic.

## Why is this needed?

- **SourceLink and Private GitLab:**  
  SourceLink enables source code debugging by mapping PDBs to source files in version control. For public repositories, this works out of the box. For private GitLab repositories, SourceLink cannot fetch source files because:
    - The legacy raw file URLs are not always supported or require authentication.
    - The GitLab API requires authentication, but does not return a `401` when credentials are missing, breaking the expected authentication flow for developer tools.

- **Improved Developer Experience:**  
  By rewriting URLs and handling authentication correctly, this proxy allows .NET developers to use SourceLink with private GitLab repositories seamlessly, just like with public repositories or other supported platforms.

## How does it work?

1. **Intercepts SourceLink requests** for raw source files.
2. **Rewrites the request URL** to the GitLab API endpoint, encoding project and file paths as needed.
3. **Handles authentication:**  
   - If the request is unauthenticated and would result in a 404 from GitLab, the proxy returns a 401 with the appropriate `WWW-Authenticate` header.
   - If credentials are present, the proxy forwards them to GitLab.
4. **Returns the source file** if authentication succeeds, or prompts for credentials if not.

## Example

- **Original SourceLink request:**  
  `/mygroup/myrepo/-/raw/main/Some/File.cs`
- **Rewritten by proxy to GitLab API:**  
  `/api/v4/projects/mygroup%2Fmyrepo/repository/files/Some%2FFile.cs/raw?ref=main`

If the request is unauthenticated, the proxy returns:

```
HTTP/1.1 401 Unauthorized
WWW-Authenticate: Basic realm="Gitlab"

