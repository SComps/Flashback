Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports System.Net
Imports System.IO
Imports System.Threading
Imports System.Text
Imports Flashback.Core

Public Class WebWorker
    Inherits BackgroundService

    Private ReadOnly _logger As ILogger(Of WebWorker)
    Private ReadOnly _registry As PrinterRegistry
    Private ReadOnly _lifetime As IHostApplicationLifetime
    Private ReadOnly _port As Integer
    Private ReadOnly _cmdFile As String
    Private _listener As HttpListener

    Public Sub New(logger As ILogger(Of WebWorker), registry As PrinterRegistry, lifetime As IHostApplicationLifetime)
        _logger = logger
        _registry = registry
        _lifetime = lifetime
        _cmdFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "commands.dat")
        Dim portStr = Environment.GetEnvironmentVariable("FLASHBACK_WEB_PORT")
        If Not Integer.TryParse(portStr, _port) Then
            _port = 8080 ' Default if somehow reached
        End If
    End Sub

    Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
        _logger.LogInformation("Flashback Web Server initializing on port {Port}...", _port)
        
        _listener = New HttpListener()
        Try
            ' Explicitly bind to both wildcard and localhost for maximum compatibility
            _listener.Prefixes.Add($"http://*:{_port}/")
            _listener.Start()
            _logger.LogInformation("Flashback Web Server active and listening at http://*:{Port}/", _port)
        Catch ex As HttpListenerException When ex.ErrorCode = 5 ' Access Denied
            _logger.LogWarning("Access Denied for *: {Port}. Falling back to localhost.", _port)
            Try
                _listener = New HttpListener()
                _listener.Prefixes.Add($"http://localhost:{_port}/")
                _listener.Start()
                _logger.LogInformation("Flashback Web Server active at http://localhost:{Port}/ (Local Only)", _port)
            Catch ex2 As Exception
                _logger.LogCritical("Web Server failed to start on localhost: {Error}", ex2.Message)
                Return
            End Try
        Catch ex As Exception
            _logger.LogCritical("Failed to start HttpListener: {Error}", ex.Message)
            Return
        End Try

        _logger.LogInformation("Web Server request loop started.")

        ' Use a registration to stop the listener immediately on cancellation, 
        ' otherwise GetContextAsync will block until the next request arrives.
        Using stoppingToken.Register(Sub()
                                         Try
                                             _listener?.Stop()
                                         Catch
                                         End Try
                                     End Sub)

            While Not stoppingToken.IsCancellationRequested
                Try
                    ' Wait for a request
                    Dim context = Await _listener.GetContextAsync()
                    ProcessRequest(context)
                Catch ex As Exception
                    If stoppingToken.IsCancellationRequested Then Exit While
                    _logger.LogError("HttpListener error in loop: {Error}", ex.Message)
                End Try
            End While
        End Using

        _listener.Stop()
        _logger.LogInformation("Flashback Web Server stopped.")
    End Function

    Private Sub ProcessRequest(context As HttpListenerContext)
        Task.Run(Async Function()
            Try
                Dim url = context.Request.Url.LocalPath
                Dim parts = url.Split("/"c, StringSplitOptions.RemoveEmptyEntries)
                
                Dim printerFilter = context.Request.QueryString("printer")
                Dim userFilter = context.Request.QueryString("subuser")
                Dim fileParam = context.Request.QueryString("file")
                
                Dim isDirectDownload = url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) AndAlso parts.Length >= 3
                
                If isDirectDownload Then
                    If String.IsNullOrEmpty(printerFilter) Then
                        printerFilter = WebUtility.UrlDecode(parts(0))
                    End If
                    If String.IsNullOrEmpty(userFilter) Then
                        userFilter = WebUtility.UrlDecode(parts(1))
                    End If
                End If
                
                ' Authentication Logic:
                ' Level 1 (All Printers) -> Public
                ' Level 2 (User folders in Printer) -> Public
                ' Level 3 (Files in User folder) -> Protected ONLY if the subuser exists in users.dat
                
                Dim user As UserInfo = Nothing
                Dim requiresAuth = False
                
                ' Determine the user-folder being accessed (from subuser param or from the file path)
                Dim targetFolder As String = userFilter
                If String.IsNullOrEmpty(targetFolder) AndAlso Not String.IsNullOrEmpty(fileParam) Then
                    ' The parent directory of the file IS the user folder
                    targetFolder = Path.GetFileName(Path.GetDirectoryName(fileParam))
                End If

                If Not String.IsNullOrEmpty(targetFolder) Then
                    Dim domainUser = $"{printerFilter}\{targetFolder}"
                    If UserManager.GetUsers().Any(Function(u) u.Username.Equals(targetFolder, StringComparison.OrdinalIgnoreCase) OrElse u.Username.Equals(domainUser, StringComparison.OrdinalIgnoreCase)) Then
                        requiresAuth = True
                    End If
                End If

                If requiresAuth Then
                    Dim authHeader = context.Request.Headers("Authorization")
                    If Not String.IsNullOrEmpty(authHeader) AndAlso authHeader.StartsWith("Basic ") Then
                        Dim creds = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Substring(6))).Split(":"c)
                        If creds.Length >= 2 Then
                            Dim inputUser = creds(0)
                            Dim inputPass = creds(1)
                            
                            ' Try exact match first
                            user = UserManager.Authenticate(inputUser, inputPass)
                            
                            ' If failed, try prefixing with printer name
                            If user Is Nothing AndAlso Not String.IsNullOrEmpty(printerFilter) AndAlso Not inputUser.Contains("\"c) Then
                                Dim prefixedUser = $"{printerFilter}\{inputUser}"
                                user = UserManager.Authenticate(prefixedUser, inputPass)
                            End If
                            
                            ' The logged in user must match the directory they are trying to access
                            Dim expectedDomainTarget = $"{printerFilter}\{targetFolder}"
                            
                            If user IsNot Nothing AndAlso Not user.Username.Equals(targetFolder, StringComparison.OrdinalIgnoreCase) AndAlso Not user.Username.Equals(expectedDomainTarget, StringComparison.OrdinalIgnoreCase) Then
                                _logger.LogWarning("Auth Failure: User {User} attempted to access folder {Folder}", user.Username, targetFolder)
                                user = Nothing
                            End If
                        End If
                    End If

                    If user Is Nothing Then
                        _logger.LogInformation("Sending 401 Challenge for {Url}", url)
                        context.Response.StatusCode = 401
                        context.Response.Headers.Add("WWW-Authenticate", "Basic realm=""Flashback Spool View""")
                        context.Response.Close()
                        Return
                    End If
                End If

                ' Normalise the path so routes work whether accessed directly (/admin)
                ' or via a reverse-proxy prefix (e.g. /printer/admin, /flashback/admin).
                ' We match on the final path segment(s) so a proxy can add any prefix it likes.
                Dim urlPath = url.TrimEnd("/"c).ToLower()
                Dim isAdminAction = urlPath.EndsWith("/admin/action") OrElse urlPath = "/admin/action"
                Dim isAdminStatus = urlPath.EndsWith("/admin/status") OrElse urlPath = "/admin/status"
                Dim isAdminLog = urlPath.EndsWith("/admin/log") OrElse urlPath = "/admin/log"
                Dim isAdmin = (urlPath.EndsWith("/admin") OrElse urlPath = "/admin") AndAlso
                              Not isAdminAction AndAlso Not isAdminStatus AndAlso Not isAdminLog
                Dim isEmail = urlPath.EndsWith("/email") OrElse urlPath = "/email"
                Dim isRoot = urlPath = "" OrElse urlPath = "/index.html" OrElse
                             (Not isAdmin AndAlso Not isAdminAction AndAlso Not isAdminStatus AndAlso
                              Not isAdminLog AndAlso Not isEmail AndAlso
                              Not isDirectDownload AndAlso parts.Length <= 1 AndAlso Not urlPath.EndsWith(".pdf"))

                If isAdmin Then
                    If Not IsAdminAuthorized(context) Then
                        context.Response.StatusCode = 401
                        context.Response.Headers.Add("WWW-Authenticate", "Basic realm=""Flashback Administration""")
                        context.Response.Close()
                        Return
                    End If
                    ServeAdminPanel(context)
                ElseIf isAdminStatus Then
                    If Not IsAdminAuthorized(context) Then
                        context.Response.StatusCode = 401
                        context.Response.Headers.Add("WWW-Authenticate", "Basic realm=""Flashback Administration""")
                        context.Response.Close()
                        Return
                    End If
                    ServeAdminStatus(context)
                ElseIf isAdminLog Then
                    If Not IsAdminAuthorized(context) Then
                        context.Response.StatusCode = 401
                        context.Response.Headers.Add("WWW-Authenticate", "Basic realm=""Flashback Administration""")
                        context.Response.Close()
                        Return
                    End If
                    ServeAdminLog(context)
                ElseIf isAdminAction Then
                    If Not IsAdminAuthorized(context) Then
                        context.Response.StatusCode = 401
                        context.Response.Headers.Add("WWW-Authenticate", "Basic realm=""Flashback Administration""")
                        context.Response.Close()
                        Return
                    End If
                    If context.Request.HttpMethod = "POST" Then
                        Await HandleAdminAction(context)
                    Else
                        context.Response.StatusCode = 405
                        context.Response.Close()
                    End If
                ElseIf url = "/" OrElse url = "/index.html" OrElse isRoot Then
                    ServeDashboard(context, user, printerFilter, userFilter)
                ElseIf isEmail Then
                    If context.Request.HttpMethod = "GET" Then
                        ServeEmailForm(context, printerFilter, userFilter, fileParam)
                    ElseIf context.Request.HttpMethod = "POST" Then
                        Await HandleEmailSubmit(context, printerFilter, userFilter, fileParam)
                    Else
                        context.Response.StatusCode = 405
                        context.Response.Close()
                    End If
                ElseIf isDirectDownload Then
                    ' Direct PDF download — auth was already enforced above via requiresAuth/user checks
                    ' because printerFilter/userFilter were populated from the URL parts (lines 89-96).
                    ' Pass the authenticated user so GetAllowedDevices can apply folder restrictions.
                    Dim printerName = WebUtility.UrlDecode(parts(0))
                    Dim subFolder = WebUtility.UrlDecode(parts(1))
                    Dim fileName = WebUtility.UrlDecode(String.Join("/", parts.Skip(2)))

                    Dim allowedDevices = GetAllowedDevices(user)
                    If allowedDevices.ContainsKey(printerName) Then
                        Dim root = allowedDevices(printerName)
                        Dim filePath = Path.Combine(root, subFolder, fileName)
                        ServeFile(context, filePath, user)
                    Else
                        _logger.LogWarning("Download rejected - printer not allowed or not found: {Printer}", printerName)
                        context.Response.StatusCode = 404
                        context.Response.Close()
                    End If
                Else
                    context.Response.StatusCode = 404
                    context.Response.Close()
                End If
            Catch ex As Exception
                _logger.LogError("Error processing request {Url}: {Error}", context.Request.Url, ex.Message)
                Try
                    context.Response.StatusCode = 500
                    context.Response.Close()
                Catch
                End Try
            End Try
        End Function)
    End Sub

    Private Sub ServeDashboard(context As HttpListenerContext, user As UserInfo, printerFilter As String, userFilter As String)
        Dim html = GenerateHtml(user, printerFilter, userFilter)
        Dim buffer = Encoding.UTF8.GetBytes(html)
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub

    Private Sub ServeFile(context As HttpListenerContext, filePath As String, Optional user As UserInfo = Nothing)
        Try
            filePath = Path.GetFullPath(filePath)

            ' Security: verify the file is within an allowed device output directory.
            ' Pass user so that folder restrictions are applied consistently.
            Dim allowedDevices = GetAllowedDevices(user)
            Dim isAllowed = allowedDevices.Values.Any(Function(root)
                Dim fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)
                Return filePath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            End Function)

            If isAllowed AndAlso File.Exists(filePath) Then
                Dim buffer = File.ReadAllBytes(filePath)
                context.Response.ContentType = "application/pdf"
                context.Response.ContentLength64 = buffer.Length
                context.Response.AddHeader("Content-Disposition", $"inline; filename=""{Path.GetFileName(filePath)}""")
                context.Response.OutputStream.Write(buffer, 0, buffer.Length)
            Else
                _logger.LogWarning("Download rejected - path not allowed or not found: {Path}", filePath)
                context.Response.StatusCode = 404
            End If
        Catch ex As Exception
            _logger.LogError("Error serving file: {Error}", ex.Message)
            context.Response.StatusCode = 500
        End Try
        context.Response.Close()
    End Sub

    Private Function GetAllowedDevices(user As UserInfo) As Dictionary(Of String, String)
        Dim devices As New Dictionary(Of String, String)
        
        ' Load devices to find output directories
        Dim configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.dat")
        If File.Exists(configFile) Then
            For Each line In File.ReadAllLines(configFile)
                Dim p = line.Split("||")
                If p.Length >= 10 Then
                    Dim devName = p(0)
                    Dim outDir = p(9)
                    If Not String.IsNullOrEmpty(outDir) AndAlso Directory.Exists(outDir) Then
                        ' Filter by HomeFolder if set and user is logged in
                        If user Is Nothing OrElse String.IsNullOrEmpty(user.HomeFolder) OrElse outDir.Contains(user.HomeFolder, StringComparison.OrdinalIgnoreCase) Then
                            If Not devices.ContainsKey(devName) Then devices.Add(devName, outDir)
                        End If
                    End If
                End If
            Next
        End If
        
        Return devices
    End Function

    Private Function GenerateHtml(user As UserInfo, printerFilter As String, userFilter As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html><html lang=""en""><head>")
        sb.AppendLine("<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        sb.AppendLine("<title>Flashback Spool Management</title>")
        sb.AppendLine($"<style>{WebAssets.Css}</style></head><body>")
        
        ' Header
        sb.AppendLine("<header><div class=""container"">")
        sb.AppendLine("<div class=""header-left"">")
        sb.AppendLine("<a href=""."" class=""logo"">Flashback</a>")
        sb.AppendLine("<h1>Spool Management</h1>")
        sb.AppendLine("</div>")
        
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy-MM-dd")
        sb.AppendLine($"<div class=""system-info"">{currentDate} {currentTime} | {If(user IsNot Nothing, user.Username, "Guest")}</div>")
        sb.AppendLine("</div></header>")
        
        sb.AppendLine("<main>")

        Dim allowedDevices = GetAllowedDevices(user)

        If String.IsNullOrEmpty(printerFilter) Then
            ' Level 1: List Printers (Public)
            sb.AppendLine("<div class=""section"">")
            sb.AppendLine("<div class=""section-header"">")
            sb.AppendLine("<h2 class=""section-title"">Available Printers</h2>")
            sb.AppendLine("</div>")
            sb.AppendLine("<div class=""section-content"">")
            
            If allowedDevices.Any() Then
                sb.AppendLine("<div class=""file-list"">")
                For Each kvp In allowedDevices
                    sb.AppendLine("<div class=""file-card"">")
                    sb.AppendLine("<div class=""file-info"">")
                    sb.AppendLine($"<a href=""?printer={WebUtility.UrlEncode(kvp.Key)}"" class=""file-name"">{WebUtility.HtmlEncode(kvp.Key)}</a>")
                    sb.AppendLine("<span class=""file-meta"">Ready • Online</span>")
                    sb.AppendLine("</div>")
                    sb.AppendLine("<div class=""file-actions"">")
                    sb.AppendLine($"<a href=""?printer={WebUtility.UrlEncode(kvp.Key)}"" class=""btn btn-primary"">View Users</a>")
                    sb.AppendLine("</div>")
                    sb.AppendLine("</div>")
                Next
                sb.AppendLine("</div>")
            Else
                sb.AppendLine("<div class=""empty-state"">No printers configured</div>")
            End If
            
            sb.AppendLine("</div></div>")
            
        ElseIf String.IsNullOrEmpty(userFilter) Then
            ' Level 2: List User Folders within Printer (Public)
            If allowedDevices.ContainsKey(printerFilter) Then
                Dim root = allowedDevices(printerFilter)
                Dim subDirs = Directory.GetDirectories(root)
                
                sb.AppendLine("<div class=""section"">")
                sb.AppendLine("<div class=""section-header"">")
                sb.AppendLine($"<h2 class=""section-title"">Users - {WebUtility.HtmlEncode(printerFilter)}</h2>")
                sb.AppendLine("</div>")
                sb.AppendLine("<div class=""section-content"">")
                
                If subDirs.Any() Then
                    sb.AppendLine("<div class=""file-list"">")
                    For Each subDir In subDirs
                        Dim dirName = Path.GetFileName(subDir)
                        Dim domainDirName = $"{printerFilter}\{dirName}"
                        Dim isProtected = UserManager.GetUsers().Any(Function(u) u.Username.Equals(dirName, StringComparison.OrdinalIgnoreCase) OrElse u.Username.Equals(domainDirName, StringComparison.OrdinalIgnoreCase))
                        
                        sb.AppendLine("<div class=""file-card"">")
                        sb.AppendLine("<div class=""file-info"">")
                        sb.AppendLine($"<a href=""?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(dirName)}"" class=""file-name"">{WebUtility.HtmlEncode(dirName)}</a>")
                        sb.AppendLine($"<span class=""file-meta"">{If(isProtected, "Protected", "Public")} folder</span>")
                        sb.AppendLine("</div>")
                        sb.AppendLine("<div class=""file-actions"">")
                        If isProtected Then
                            sb.AppendLine("<span class=""badge-locked"">Protected</span>")
                        End If
                        sb.AppendLine($"<a href=""?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(dirName)}"" class=""btn btn-primary"">View Files</a>")
                        sb.AppendLine("</div>")
                        sb.AppendLine("</div>")
                    Next
                    sb.AppendLine("</div>")
                Else
                    sb.AppendLine("<div class=""empty-state"">No user folders found</div>")
                End If
                
                sb.AppendLine("</div></div>")
            End If
            
        Else
            ' Level 3: List Files for specific sub-user (Conditional Auth)
            If allowedDevices.ContainsKey(printerFilter) Then
                Dim root = allowedDevices(printerFilter)
                Dim targetDir = Path.Combine(root, userFilter)
                
                If Directory.Exists(targetDir) Then
                    Dim files = Directory.GetFiles(targetDir, "*.pdf", SearchOption.TopDirectoryOnly) _
                                .Select(Function(f) New FileInfo(f)) _
                                .OrderByDescending(Function(f) f.LastWriteTime)
                    
                    sb.AppendLine("<div class=""section"">")
                    sb.AppendLine("<div class=""section-header"">")
                    sb.AppendLine($"<h2 class=""section-title"">Documents - {WebUtility.HtmlEncode(userFilter)}</h2>")
                    sb.AppendLine("</div>")
                    sb.AppendLine("<div class=""section-content"">")
                    
                    If files.Any() Then
                        sb.AppendLine("<div class=""file-list"">")
                        For Each fi In files
                            Dim downloadUrl = $"{WebUtility.UrlEncode(printerFilter)}/{WebUtility.UrlEncode(userFilter)}/{WebUtility.UrlEncode(fi.Name)}"
                            Dim emailUrl = $"/email?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(userFilter)}&file={WebUtility.UrlEncode(fi.Name)}"
                            Dim sizeMb = fi.Length / (1024.0 * 1024.0)
                            
                            sb.AppendLine("<div class=""file-card"">")
                            sb.AppendLine("<div class=""file-info"">")
                            sb.AppendLine($"<a href=""{downloadUrl}"" target=""_blank"" class=""file-name"">{WebUtility.HtmlEncode(fi.Name)}</a>")
                            sb.AppendLine($"<span class=""file-meta"">{sizeMb:F2} MB • {fi.LastWriteTime:yyyy-MM-dd HH:mm}</span>")
                            sb.AppendLine("</div>")
                            sb.AppendLine("<div class=""file-actions"">")
                            sb.AppendLine($"<a href=""{emailUrl}"" class=""btn btn-secondary"">Email</a>")
                            sb.AppendLine($"<a href=""{downloadUrl}"" target=""_blank"" class=""btn btn-primary"">Download</a>")
                            sb.AppendLine("</div>")
                            sb.AppendLine("</div>")
                        Next
                        sb.AppendLine("</div>")
                    Else
                        sb.AppendLine("<div class=""empty-state"">No documents found</div>")
                    End If
                    
                    sb.AppendLine("</div></div>")
                End If
            End If
        End If

        sb.AppendLine("</main>")
        sb.AppendLine("<div class=""status-bar"">Flashback Spool Management System v1.0</div>")
        sb.AppendLine("</body></html>")
        Return sb.ToString()
    End Function

    Private Sub ServeEmailForm(context As HttpListenerContext, printerFilter As String, userFilter As String, fileName As String)
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html><html lang=""en""><head>")
        sb.AppendLine("<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        sb.AppendLine("<title>Email Document - Flashback</title>")
        sb.AppendLine($"<style>{WebAssets.Css}</style></head><body>")
        
        ' Header
        sb.AppendLine("<header><div class=""container"">")
        sb.AppendLine("<div class=""header-left"">")
        sb.AppendLine("<a href=""."" class=""logo"">Flashback</a>")
        sb.AppendLine("<h1>Email Document</h1>")
        sb.AppendLine("</div>")
        
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy-MM-dd")
        sb.AppendLine($"<div class=""system-info"">{currentDate} {currentTime}</div>")
        sb.AppendLine("</div></header>")
        
        sb.AppendLine("<main>")
        sb.AppendLine("<div class=""section"">")
        sb.AppendLine("<div class=""section-header"">")
        sb.AppendLine("<h2 class=""section-title"">Send Document via Email</h2>")
        sb.AppendLine("</div>")
        sb.AppendLine("<div class=""section-content"" style=""padding: 24px;"">")
        sb.AppendLine($"<p style=""margin-bottom: 24px; color: #525252;"">File: <strong>{WebUtility.HtmlEncode(fileName)}</strong></p>")
        
        sb.AppendLine($"<form method=""POST"" action=""email?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(userFilter)}&file={WebUtility.UrlEncode(fileName)}"">")
        
        sb.AppendLine("<label for=""email"">Recipient Email Address</label>")
        sb.AppendLine("<input type=""email"" id=""email"" name=""email"" required placeholder=""user@example.com"" />")
        
        sb.AppendLine("<label for=""subject"">Subject</label>")
        sb.AppendLine($"<input type=""text"" id=""subject"" name=""subject"" value=""Flashback Spool: {WebUtility.HtmlEncode(fileName)}"" />")
        
        sb.AppendLine("<label for=""message"">Message</label>")
        sb.AppendLine("<textarea id=""message"" name=""message"" rows=""5"">Please find the attached PDF document from the Flashback spool system.</textarea>")
        
        sb.AppendLine("<div style=""display: flex; gap: 12px; margin-top: 24px;"">")
        sb.AppendLine("<button type=""submit"" class=""btn btn-primary"">Send Email</button>")
        sb.AppendLine($"<a href=""?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(userFilter)}"" class=""btn btn-secondary"">Cancel</a>")
        sb.AppendLine("</div>")
        sb.AppendLine("</form>")
        
        sb.AppendLine("</div></div>")
        sb.AppendLine("</main>")
        sb.AppendLine("<div class=""status-bar"">Flashback Spool Management System v1.0</div>")
        sb.AppendLine("</body></html>")
        
        Dim buffer = Encoding.UTF8.GetBytes(sb.ToString())
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub

    Private Async Function HandleEmailSubmit(context As HttpListenerContext, printerFilter As String, userFilter As String, fileName As String) As Task
        Try
            ' Read POST data
            Dim body As String
            Using reader As New StreamReader(context.Request.InputStream, context.Request.ContentEncoding)
                body = reader.ReadToEnd()
            End Using

            ' Parse form data
            Dim formData = System.Web.HttpUtility.ParseQueryString(body)
            Dim recipientEmail = formData("email")
            Dim subject = formData("subject")
            Dim message = formData("message")

            If String.IsNullOrWhiteSpace(recipientEmail) Then
                ServeErrorPage(context, "Email address is required")
                Return
            End If

            ' Find the file
            Dim allowedDevices = GetAllowedDevices(Nothing)
            If Not allowedDevices.ContainsKey(printerFilter) Then
                ServeErrorPage(context, "Printer not found")
                Return
            End If

            Dim root = allowedDevices(printerFilter)
            Dim filePath = Path.Combine(root, userFilter, fileName)

            If Not File.Exists(filePath) Then
                ServeErrorPage(context, "File not found")
                Return
            End If

            ' Load device configuration to get email settings
            Dim configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.dat")
            Dim device As Devs = Nothing

            If File.Exists(configFile) Then
                For Each line In File.ReadAllLines(configFile)
                    Dim p = line.Split("||")
                    If p.Length >= 10 AndAlso p(0).Equals(printerFilter, StringComparison.OrdinalIgnoreCase) Then
                        device = New Devs With {
                            .DevName = p(0),
                            .SmtpServer = If(p.Length > 15, p(15), ""),
                            .SmtpPort = If(p.Length > 16 AndAlso Integer.TryParse(p(16), Nothing), CInt(p(16)), 587),
                            .SmtpUsername = If(p.Length > 17, p(17), ""),
                            .SmtpPassword = If(p.Length > 18, p(18), ""),
                            .SmtpUseTLS = If(p.Length > 19, p(19).Equals("true", StringComparison.OrdinalIgnoreCase), True),
                            .EmailFromAddress = If(p.Length > 20, p(20), ""),
                            .EmailFromName = If(p.Length > 21, p(21), "Flashback Spool System")
                        }
                        Exit For
                    End If
                Next
            End If

            If device Is Nothing OrElse String.IsNullOrEmpty(device.SmtpServer) Then
                ServeErrorPage(context, "Email is not configured for this printer. Please contact your administrator.")
                Return
            End If

            ' Send email
            Dim emailConfig As New Flashback.Core.EmailConfig With {
                .SmtpServer = device.SmtpServer,
                .SmtpPort = device.SmtpPort,
                .SmtpUsername = device.SmtpUsername,
                .SmtpPassword = device.SmtpPassword,
                .UseTLS = device.SmtpUseTLS,
                .FromAddress = device.EmailFromAddress,
                .FromName = device.EmailFromName,
                .Subject = If(String.IsNullOrWhiteSpace(subject), $"Flashback Spool: {fileName}", subject),
                .Body = If(String.IsNullOrWhiteSpace(message), "Please find the attached PDF document.", message)
            }
            emailConfig.SetRecipientsFromString(recipientEmail)

            Dim emailService As New Flashback.Core.EmailService(New Flashback.Core.FileLogger(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "printers.log")))
            Dim success = Await emailService.SendPdfEmailAsync(emailConfig, filePath, fileName, device.DevName, userFilter, 0)

            If success Then
                ServeSuccessPage(context, recipientEmail, printerFilter, userFilter)
            Else
                ServeErrorPage(context, "Failed to send email. Please check the logs for details.")
            End If

        Catch ex As Exception
            _logger.LogError("Error sending email: {Error}", ex.Message)
            ServeErrorPage(context, $"Error: {ex.Message}")
        End Try
    End Function

    Private Sub ServeSuccessPage(context As HttpListenerContext, email As String, printerFilter As String, userFilter As String)
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html><html lang=""en""><head>")
        sb.AppendLine("<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        sb.AppendLine("<title>Email Sent - Flashback</title>")
        sb.AppendLine($"<style>{WebAssets.Css}</style></head><body>")
        
        ' Header
        sb.AppendLine("<header><div class=""container"">")
        sb.AppendLine("<div class=""header-left"">")
        sb.AppendLine("<a href=""."" class=""logo"">Flashback</a>")
        sb.AppendLine("<h1>Email Sent</h1>")
        sb.AppendLine("</div>")
        
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy-MM-dd")
        sb.AppendLine($"<div class=""system-info"">{currentDate} {currentTime}</div>")
        sb.AppendLine("</div></header>")
        
        sb.AppendLine("<main>")
        sb.AppendLine("<div class=""section"">")
        sb.AppendLine("<div class=""section-header"">")
        sb.AppendLine("<h2 class=""section-title"">Success</h2>")
        sb.AppendLine("</div>")
        sb.AppendLine("<div class=""section-content"" style=""padding: 24px;"">")
        sb.AppendLine($"<p style=""color: #24a148; font-size: 1rem; margin-bottom: 16px; font-weight: 600;"">✓ Email sent successfully</p>")
        sb.AppendLine($"<p style=""color: #525252; margin-bottom: 24px;"">The PDF document has been sent to <strong>{WebUtility.HtmlEncode(email)}</strong></p>")
        sb.AppendLine($"<a href=""?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(userFilter)}"" class=""btn btn-primary"">Return to Documents</a>")
        sb.AppendLine("</div></div>")
        sb.AppendLine("</main>")
        sb.AppendLine("<div class=""status-bar"">Flashback Spool Management System v1.0</div>")
        sb.AppendLine("</body></html>")
        
        Dim buffer = Encoding.UTF8.GetBytes(sb.ToString())
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub

    Private Sub ServeErrorPage(context As HttpListenerContext, errorMessage As String)
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html><html lang=""en""><head>")
        sb.AppendLine("<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        sb.AppendLine("<title>Error - Flashback</title>")
        sb.AppendLine($"<style>{WebAssets.Css}</style></head><body>")
        
        ' Header
        sb.AppendLine("<header><div class=""container"">")
        sb.AppendLine("<div class=""header-left"">")
        sb.AppendLine("<a href=""."" class=""logo"">Flashback</a>")
        sb.AppendLine("<h1>Error</h1>")
        sb.AppendLine("</div>")
        
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy-MM-dd")
        sb.AppendLine($"<div class=""system-info"">{currentDate} {currentTime}</div>")
        sb.AppendLine("</div></header>")
        
        sb.AppendLine("<main>")
        sb.AppendLine("<div class=""section"">")
        sb.AppendLine("<div class=""section-header"">")
        sb.AppendLine("<h2 class=""section-title"">Error</h2>")
        sb.AppendLine("</div>")
        sb.AppendLine("<div class=""section-content"" style=""padding: 24px;"">")
        sb.AppendLine($"<p style=""color: #da1e28; font-size: 1rem; margin-bottom: 16px; font-weight: 600;"">✗ An error occurred</p>")
        sb.AppendLine($"<p style=""color: #525252; margin-bottom: 24px;"">{WebUtility.HtmlEncode(errorMessage)}</p>")
        sb.AppendLine("<a href=""javascript:history.back()"" class=""btn btn-primary"">Go Back</a>")
        sb.AppendLine("</div></div>")
        sb.AppendLine("</main>")
        sb.AppendLine("<div class=""status-bar"">Flashback Spool Management System v1.0</div>")
        sb.AppendLine("</body></html>")
        
        Dim buffer = Encoding.UTF8.GetBytes(sb.ToString())
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub
    ' ---------------------------------------------------------------------------
    ' Admin Panel — helpers, HTML, action handler
    ' ---------------------------------------------------------------------------

    ''' <summary>
    ''' Returns True if the request is authorized to access the admin panel.
    ''' If no syspw is configured, all requests are allowed.
    ''' If a syspw is configured, requires HTTP Basic Auth with username "admin"
    ''' and the syspw as the password.
    ''' </summary>
    Private Function IsAdminAuthorized(context As HttpListenerContext) As Boolean
        Dim syspw = SecurityUtils.ReadSyspw(AppDomain.CurrentDomain.BaseDirectory)
        If String.IsNullOrEmpty(syspw) Then Return True  ' No password configured — open access

        Dim authHeader = context.Request.Headers("Authorization")
        If String.IsNullOrEmpty(authHeader) OrElse Not authHeader.StartsWith("Basic ") Then
            Return False
        End If

        Try
            Dim decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Substring(6)))
            Dim colon = decoded.IndexOf(":"c)
            If colon < 0 Then Return False
            Dim inputUser = decoded.Substring(0, colon)
            Dim inputPass = decoded.Substring(colon + 1)
            Return inputUser.Equals("admin", StringComparison.OrdinalIgnoreCase) AndAlso inputPass.Trim() = syspw
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Serves the /admin GET page.
    ''' </summary>
    Private Sub ServeAdminPanel(context As HttpListenerContext)
        Dim html = GenerateAdminHtml()
        Dim buffer = Encoding.UTF8.GetBytes(html)
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub

    ''' <summary>
    ''' Generates the full admin panel HTML page.
    ''' Lists all printers from devices.dat with live status from PrinterRegistry.
    ''' Includes Engine Controls (Stop / Restart) at the top.
    ''' </summary>
    Private Function GenerateAdminHtml() As String
        Dim sb As New StringBuilder()
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy-MM-dd")

        sb.AppendLine("<!DOCTYPE html><html lang=""en""><head>")
        sb.AppendLine("<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        sb.AppendLine("<title>Flashback Administration</title>")
        sb.AppendLine($"<style>{WebAssets.Css}</style>")
        sb.AppendLine("<script>")
        sb.AppendLine("(function() {")
        sb.AppendLine("  'use strict';")
        sb.AppendLine("")
        sb.AppendLine("  // Derive absolute base URL for admin API calls.")
        sb.AppendLine("  // window.location.pathname is e.g. /printer/admin — we use it directly.")
        sb.AppendLine("  var adminBase = window.location.pathname.replace(/\/+$/, '');")
        sb.AppendLine("  // Ensure it ends with /admin (strip any trailing segment that isn't 'admin')")
        sb.AppendLine("  if (adminBase.split('/').pop() !== 'admin') {")
        sb.AppendLine("    adminBase = adminBase + '/admin';")
        sb.AppendLine("  }")
        sb.AppendLine("")
        sb.AppendLine("  // --- Status auto-refresh (every 5 seconds) ---")
        sb.AppendLine("  function refreshStatus() {")
        sb.AppendLine("    fetch(adminBase + '/status')")
        sb.AppendLine("      .then(function(r) { return r.ok ? r.json() : null; })")
        sb.AppendLine("      .then(function(data) {")
        sb.AppendLine("        if (!data) return;")
        sb.AppendLine("        // Update each printer row badge and buttons")
        sb.AppendLine("        data.printers.forEach(function(p) {")
        sb.AppendLine("          var row = document.getElementById(p.rowId);")
        sb.AppendLine("          if (!row) return;")
        sb.AppendLine("          var badge = row.querySelector('.pr-badge');")
        sb.AppendLine("          var actionsDiv = document.getElementById(p.rowId + '-actions');")
        sb.AppendLine("          if (badge) { badge.className = 'pr-badge ' + p.badgeClass; badge.textContent = p.status; }")
        sb.AppendLine("          if (actionsDiv) { actionsDiv.innerHTML = badge ? badge.outerHTML + p.actionsHtml : p.actionsHtml; }")
        sb.AppendLine("        });")
        sb.AppendLine("        // Update header count")
        sb.AppendLine("        var hdr = document.getElementById('pr-header');")
        sb.AppendLine("        if (hdr) hdr.textContent = 'Printers (' + data.configured + ' configured, ' + data.active + ' active)';")
        sb.AppendLine("        // Update clock")
        sb.AppendLine("        var clk = document.getElementById('admin-clock');")
        sb.AppendLine("        if (clk) clk.textContent = data.time;")
        sb.AppendLine("      })")
        sb.AppendLine("      .catch(function() {});  // silently ignore network errors")
        sb.AppendLine("  }")
        sb.AppendLine("")
        sb.AppendLine("  // --- Log tail (every 5 seconds) ---")
        sb.AppendLine("  var logUserScrolled = false;")
        sb.AppendLine("  function refreshLog() {")
        sb.AppendLine("    fetch(adminBase + '/log')")
        sb.AppendLine("      .then(function(r) { return r.ok ? r.text() : null; })")
        sb.AppendLine("      .then(function(text) {")
        sb.AppendLine("        if (text === null) return;")
        sb.AppendLine("        var el = document.getElementById('log-tail');")
        sb.AppendLine("        if (!el) return;")
        sb.AppendLine("        // Colour-code lines by level")
        sb.AppendLine("        var lines = text.split('\n');")
        sb.AppendLine("        var html = lines.map(function(line) {")
        sb.AppendLine("          var cls = 'log-line';")
        sb.AppendLine("          if (line.indexOf('[Error]') >= 0 || line.indexOf('[Critical]') >= 0) cls += ' log-error';")
        sb.AppendLine("          else if (line.indexOf('[Warning]') >= 0) cls += ' log-warn';")
        sb.AppendLine("          else if (line.indexOf('[Debug]') >= 0 || line.indexOf('[Trace]') >= 0) cls += ' log-debug';")
        sb.AppendLine("          var escaped = line.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');")
        sb.AppendLine("          return '<span class=""' + cls + '"">' + escaped + '\n</span>';")
        sb.AppendLine("        }).join('');")
        sb.AppendLine("        el.innerHTML = html;")
        sb.AppendLine("        // Auto-scroll to bottom unless user has scrolled up")
        sb.AppendLine("        if (!logUserScrolled) el.scrollTop = el.scrollHeight;")
        sb.AppendLine("      })")
        sb.AppendLine("      .catch(function() {});")
        sb.AppendLine("  }")
        sb.AppendLine("")
        sb.AppendLine("  // Track manual scroll so we stop auto-scrolling when user reads up")
        sb.AppendLine("  document.addEventListener('DOMContentLoaded', function() {")
        sb.AppendLine("    var el = document.getElementById('log-tail');")
        sb.AppendLine("    if (el) {")
        sb.AppendLine("      el.addEventListener('scroll', function() {")
        sb.AppendLine("        logUserScrolled = (el.scrollTop + el.clientHeight) < (el.scrollHeight - 20);")
        sb.AppendLine("      });")
        sb.AppendLine("    }")
        sb.AppendLine("    refreshStatus();")
        sb.AppendLine("    refreshLog();")
        sb.AppendLine("    setInterval(refreshStatus, 5000);")
        sb.AppendLine("    setInterval(refreshLog, 5000);")
        sb.AppendLine("  });")
        sb.AppendLine("})();")
        sb.AppendLine("</script>")
        sb.AppendLine("</head><body>")

        ' Header
        sb.AppendLine("<header><div class=""container"">")
        sb.AppendLine("<div class=""header-left"">")
        sb.AppendLine("<a href="".."" class=""logo"">Flashback</a>")
        sb.AppendLine("<h1>Administration</h1>")
        sb.AppendLine("</div>")
        sb.AppendLine($"<div class=""system-info""><span id=""admin-clock"">{currentDate} {currentTime}</span> | Admin</div>")
        sb.AppendLine("</div></header>")

        sb.AppendLine("<main>")

        ' --- Engine Controls section ---
        sb.AppendLine("<div class=""section"">")
        sb.AppendLine("<div class=""section-header"">")
        sb.AppendLine("<h2 class=""section-title"">Engine Controls</h2>")
        sb.AppendLine("</div>")
        sb.AppendLine("<div class=""section-content"" style=""padding: 20px 24px;"">")
        sb.AppendLine("<p style=""color: #525252; font-size: 0.875rem; margin-bottom: 16px;"">")
        sb.AppendLine("Use these controls to restart all printer connections or stop the engine process.")
        sb.AppendLine("</p>")
        sb.AppendLine("<div style=""display: flex; gap: 12px; flex-wrap: wrap;"">")

        ' Restart button — red/danger to signal destructive action
        sb.AppendLine("<form method=""POST"" action=""action"" style=""display:inline;"">")
        sb.AppendLine("<input type=""hidden"" name=""cmd"" value=""restart"" />")
        sb.AppendLine("<button type=""submit"" class=""btn btn-danger"" onclick=""return confirm('Restart the Flashback Engine? All printer connections will be briefly interrupted.')"">")
        sb.AppendLine("&#x21BA; Restart Engine")
        sb.AppendLine("</button>")
        sb.AppendLine("</form>")

        ' Stop button — darker red to distinguish from restart
        sb.AppendLine("<form method=""POST"" action=""action"" style=""display:inline;"">")
        sb.AppendLine("<input type=""hidden"" name=""cmd"" value=""stop"" />")
        sb.AppendLine("<button type=""submit"" class=""btn btn-danger btn-danger-dark"" onclick=""return confirm('Stop the Flashback Engine? The service will terminate.')"">")
        sb.AppendLine("&#x25A0; Stop Engine")
        sb.AppendLine("</button>")
        sb.AppendLine("</form>")

        sb.AppendLine("</div>")
        sb.AppendLine("</div></div>")

        ' --- Printer List section ---
        ' Load all configured printers from devices.dat into simple parallel lists
        ' (avoids VB.NET named-tuple runtime issues)
        Dim configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.dat")
        Dim prNames As New List(Of String)
        Dim prDescs As New List(Of String)
        Dim prConnTypes As New List(Of Integer)
        Dim prDests As New List(Of String)
        Dim prEnabled As New List(Of Boolean)
        Dim separator() As String = {"||"}

        If File.Exists(configFile) Then
            For Each line In File.ReadAllLines(configFile)
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim p = line.Split(separator, StringSplitOptions.None)
                If p.Length < 10 Then Continue For
                Dim isEnabled = If(p.Length >= 13, p(12) = "True", True)
                Dim connType As Integer
                Integer.TryParse(p(3), connType)
                prNames.Add(p(0))
                prDescs.Add(p(1))
                prConnTypes.Add(connType)
                prDests.Add(p(4))
                prEnabled.Add(isEnabled)
            Next
        End If

        ' Build a lookup of live devices by name.
        ' Use a manual loop instead of .ToDictionary() to safely handle the case where
        ' a device name appears more than once in the registry during a reconnect race
        ' (duplicate key would cause .ToDictionary() to throw).
        Dim liveDevices As New Dictionary(Of String, Devs)(StringComparer.OrdinalIgnoreCase)
        For Each d In _registry.GetSnapshot()
            liveDevices(d.DevName) = d   ' overwrite on duplicate — last writer wins
        Next

        sb.AppendLine("<div class=""section"">")
        sb.AppendLine("<div class=""section-header"">")
        sb.AppendLine($"<h2 class=""section-title"" id=""pr-header"">Printers ({prNames.Count} configured, {liveDevices.Count} active)</h2>")
        sb.AppendLine("</div>")
        sb.AppendLine("<div class=""section-content"">")

        If prNames.Count > 0 Then
            sb.AppendLine("<div class=""file-list"">")
            For i As Integer = 0 To prNames.Count - 1
                Dim prName = prNames(i)
                Dim prDesc = prDescs(i)
                Dim prConnType = prConnTypes(i)
                Dim prDest = prDests(i)
                Dim prIsEnabled = prEnabled(i)

                Dim statusBadge As String
                Dim badgeClass As String
                Dim actionsHtml As String

                If Not prIsEnabled Then
                    badgeClass = "badge-disabled"
                    statusBadge = $"<span class=""pr-badge {badgeClass}"">Disabled</span>"
                    actionsHtml = ""
                ElseIf liveDevices.ContainsKey(prName) Then
                    Dim dev = liveDevices(prName)
                    If dev.Connected Then
                        badgeClass = "badge-connected"
                        statusBadge = $"<span class=""pr-badge {badgeClass}"">Connected</span>"
                        actionsHtml = $"<form method=""POST"" action=""action"" style=""display:inline; margin-left:8px;""><input type=""hidden"" name=""cmd"" value=""disconnect"" /><input type=""hidden"" name=""dev"" value=""{WebUtility.HtmlEncode(prName)}"" /><button type=""submit"" class=""btn btn-secondary"">Stop</button></form>"
                    ElseIf dev.Connecting Then
                        badgeClass = "badge-connecting"
                        statusBadge = $"<span class=""pr-badge {badgeClass}"">Connecting...</span>"
                        actionsHtml = $"<form method=""POST"" action=""action"" style=""display:inline; margin-left:8px;""><input type=""hidden"" name=""cmd"" value=""disconnect"" /><input type=""hidden"" name=""dev"" value=""{WebUtility.HtmlEncode(prName)}"" /><button type=""submit"" class=""btn btn-secondary"">Stop</button></form>"
                    Else
                        badgeClass = "badge-disconnected"
                        statusBadge = $"<span class=""pr-badge {badgeClass}"">Disconnected</span>"
                        actionsHtml = $"<form method=""POST"" action=""action"" style=""display:inline; margin-left:8px;""><input type=""hidden"" name=""cmd"" value=""connect"" /><input type=""hidden"" name=""dev"" value=""{WebUtility.HtmlEncode(prName)}"" /><button type=""submit"" class=""btn btn-primary"">Start</button></form>"
                    End If
                Else
                    badgeClass = "badge-disconnected"
                    statusBadge = $"<span class=""pr-badge {badgeClass}"">Stopped</span>"
                    actionsHtml = $"<form method=""POST"" action=""action"" style=""display:inline; margin-left:8px;""><input type=""hidden"" name=""cmd"" value=""connect"" /><input type=""hidden"" name=""dev"" value=""{WebUtility.HtmlEncode(prName)}"" /><button type=""submit"" class=""btn btn-primary"">Start</button></form>"
                End If

                Dim connTypeName = If(prConnType = 3, "Listener", "Client")
                Dim encodedName = WebUtility.HtmlEncode(prName)
                ' Safe DOM id: base64-like encoding using just the name; JS uses this to find the row
                Dim rowId = "pr-" & WebUtility.UrlEncode(prName).Replace("%", "_")

                sb.AppendLine($"<div class=""file-card"" id=""{rowId}"">")
                sb.AppendLine("<div class=""file-info"">")
                sb.AppendLine($"<span class=""file-name"">{encodedName}</span>")
                sb.AppendLine($"<span class=""file-meta"">{WebUtility.HtmlEncode(prDesc)} &nbsp;&bull;&nbsp; {connTypeName}: {WebUtility.HtmlEncode(prDest)}</span>")
                sb.AppendLine("</div>")
                sb.AppendLine($"<div class=""file-actions"" id=""{rowId}-actions"">")
                sb.AppendLine(statusBadge)
                sb.AppendLine(actionsHtml)
                sb.AppendLine("</div>")
                sb.AppendLine("</div>")
            Next
            sb.AppendLine("</div>")
        Else
            sb.AppendLine("<div class=""empty-state"">No printers configured. Add printers via the configuration tools.</div>")
        End If

        sb.AppendLine("</div></div>")

        ' --- Log Tail section ---
        sb.AppendLine("<div class=""section"">")
        sb.AppendLine("<div class=""section-header"" style=""display:flex; align-items:center; justify-content:space-between;"">")
        sb.AppendLine("<h2 class=""section-title"">Engine Log</h2>")
        sb.AppendLine("<span style=""font-size:0.75rem; color:#525252; padding-right:16px;"">Last 100 lines &bull; updates every 5s</span>")
        sb.AppendLine("</div>")
        sb.AppendLine("<div class=""section-content"" style=""padding:0;"">")
        sb.AppendLine("<pre id=""log-tail"" class=""log-tail"">Loading log...</pre>")
        sb.AppendLine("</div></div>")

        sb.AppendLine("</main>")
        sb.AppendLine("<div class=""status-bar"">Flashback Administration Panel &nbsp;&bull;&nbsp; <a href="".."" style=""color:#525252;"">Spool Management</a></div>")
        sb.AppendLine("</body></html>")
        Return sb.ToString()
    End Function

    ''' <summary>
    ''' Handles POST /admin/action.
    ''' cmd=connect|disconnect  → writes to commands.dat, redirects back to /admin
    ''' cmd=stop                → graceful shutdown, serves confirmation page
    ''' cmd=restart             → writes restart.req, graceful shutdown, serves confirmation page
    ''' </summary>
    Private Async Function HandleAdminAction(context As HttpListenerContext) As Task
        Try
            Dim body As String
            Using reader As New StreamReader(context.Request.InputStream, context.Request.ContentEncoding)
                body = Await reader.ReadToEndAsync()
            End Using

            ' Derive the admin panel base URL from the incoming request path.
            ' The form POSTs to .../admin/action; strip "action" to get .../admin.
            ' This preserves any reverse-proxy prefix (e.g. /printer/admin).
            Dim requestPath = context.Request.Url.AbsolutePath.TrimEnd("/"c)
            Dim adminBase As String
            If requestPath.EndsWith("/action", StringComparison.OrdinalIgnoreCase) Then
                adminBase = requestPath.Substring(0, requestPath.Length - "/action".Length)
            Else
                adminBase = requestPath
            End If
            ' Reconstruct the full public-facing admin URL for redirects / meta-refresh
            Dim adminUrl = $"{context.Request.Url.Scheme}://{context.Request.Url.Authority}{adminBase}"

            Dim formData = System.Web.HttpUtility.ParseQueryString(body)
            Dim cmd = If(formData("cmd"), "").Trim().ToLower()
            Dim dev = If(formData("dev"), "").Trim()

            Select Case cmd
                Case "connect", "disconnect"
                    If String.IsNullOrEmpty(dev) Then
                        ServeAdminMessage(context, "Error", "No device name specified.", "#da1e28", False)
                        Return
                    End If
                    Dim cmdLine = $"{cmd.ToUpper()}||{dev}"
                    File.AppendAllText(_cmdFile, cmdLine & Environment.NewLine)
                    _logger.LogInformation("Admin panel: queued {Cmd} for device {Dev}", cmd.ToUpper(), dev)
                    ' Redirect back to admin panel so the user sees the updated status
                    context.Response.StatusCode = 302
                    context.Response.Headers.Add("Location", adminUrl)
                    context.Response.Close()

                Case "stop"
                    _logger.LogInformation("Admin panel: engine stop requested.")
                    ServeAdminMessage(context, "Engine Stopping",
                        "The Flashback Engine is shutting down. All printer connections will be closed.",
                        "#da1e28", False, adminUrl)
                    ' Brief delay so the HTTP response is fully flushed before the host stops
                    Await Task.Delay(500)
                    _lifetime.StopApplication()

                Case "restart"
                    _logger.LogInformation("Admin panel: engine restart requested.")
                    ' Write the sentinel file so Program.vb re-launches after shutdown
                    Dim restartFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "restart.req")
                    File.WriteAllText(restartFile, DateTime.Now.ToString("o"))
                    ServeAdminMessage(context, "Engine Restarting",
                        "The Flashback Engine is restarting. All printer connections will be re-established shortly. Refresh this page in a few seconds.",
                        "#d97706", True, adminUrl)
                    ' Brief delay so the HTTP response is fully flushed before the host stops
                    Await Task.Delay(500)
                    _lifetime.StopApplication()

                Case Else
                    context.Response.StatusCode = 400
                    context.Response.Close()
            End Select
        Catch ex As Exception
            _logger.LogError("Error in HandleAdminAction: {Error}", ex.Message)
            Try
                context.Response.StatusCode = 500
                context.Response.Close()
            Catch
            End Try
        End Try
    End Function

    ''' <summary>
    ''' Serves a simple styled confirmation/information page for admin actions.
    ''' </summary>
    Private Sub ServeAdminMessage(context As HttpListenerContext, title As String, message As String, accentColor As String, showRefresh As Boolean, Optional adminUrl As String = "/admin")
        Dim sb As New StringBuilder()
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy-MM-dd")

        sb.AppendLine("<!DOCTYPE html><html lang=""en""><head>")
        sb.AppendLine("<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        sb.AppendLine($"<title>{WebUtility.HtmlEncode(title)} - Flashback</title>")
        If showRefresh Then
            sb.AppendLine($"<meta http-equiv=""refresh"" content=""8;url={WebUtility.HtmlEncode(adminUrl)}"" />")
        End If
        sb.AppendLine($"<style>{WebAssets.Css}</style></head><body>")

        sb.AppendLine("<header><div class=""container"">")
        sb.AppendLine("<div class=""header-left"">")
        sb.AppendLine("<a href="".."" class=""logo"">Flashback</a>")
        sb.AppendLine($"<h1>{WebUtility.HtmlEncode(title)}</h1>")
        sb.AppendLine("</div>")
        sb.AppendLine($"<div class=""system-info"">{currentDate} {currentTime} | Admin</div>")
        sb.AppendLine("</div></header>")

        sb.AppendLine("<main><div class=""section"">")
        sb.AppendLine("<div class=""section-header"">")
        sb.AppendLine($"<h2 class=""section-title"">{WebUtility.HtmlEncode(title)}</h2>")
        sb.AppendLine("</div>")
        sb.AppendLine("<div class=""section-content"" style=""padding: 24px;"">")
        sb.AppendLine($"<p style=""color: {accentColor}; font-size: 1rem; margin-bottom: 16px; font-weight: 600;"">")
        sb.AppendLine(WebUtility.HtmlEncode(message))
        sb.AppendLine("</p>")
        If showRefresh Then
            sb.AppendLine("<p style=""color: #525252; font-size: 0.875rem;"">This page will automatically refresh in a few seconds.</p>")
            sb.AppendLine($"<a href=""{WebUtility.HtmlEncode(adminUrl)}"" class=""btn btn-primary"" style=""margin-top:16px;"">Return to Administration</a>")
        End If
        sb.AppendLine("</div></div></main>")
        sb.AppendLine("<div class=""status-bar"">Flashback Administration Panel</div>")
        sb.AppendLine("</body></html>")

        Dim buffer = Encoding.UTF8.GetBytes(sb.ToString())
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub

    ' ---------------------------------------------------------------------------
    ' Admin API endpoints — /admin/status (JSON) and /admin/log (plain text)
    ' ---------------------------------------------------------------------------

    ''' <summary>
    ''' GET /admin/status — returns JSON with live printer statuses and clock time.
    ''' Consumed by the admin panel JavaScript every 5 seconds for live updates.
    ''' </summary>
    Private Sub ServeAdminStatus(context As HttpListenerContext)
        Try
            Dim separator() As String = {"||"}
            Dim configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "devices.dat")
            Dim prNames As New List(Of String)
            Dim prEnabled As New List(Of Boolean)

            If File.Exists(configFile) Then
                For Each line In File.ReadAllLines(configFile)
                    If String.IsNullOrWhiteSpace(line) Then Continue For
                    Dim p = line.Split(separator, StringSplitOptions.None)
                    If p.Length < 10 Then Continue For
                    prNames.Add(p(0))
                    prEnabled.Add(If(p.Length >= 13, p(12) = "True", True))
                Next
            End If

            Dim liveDevices As New Dictionary(Of String, Devs)(StringComparer.OrdinalIgnoreCase)
            For Each d In _registry.GetSnapshot()
                liveDevices(d.DevName) = d
            Next

            Dim sb As New StringBuilder()
            sb.AppendLine("{")
            sb.AppendLine($"  ""time"": ""{DateTime.Now:yyyy-MM-dd HH:mm:ss}"",")
            sb.AppendLine($"  ""configured"": {prNames.Count},")
            sb.AppendLine($"  ""active"": {liveDevices.Count},")
            sb.AppendLine("  ""printers"": [")

            For i As Integer = 0 To prNames.Count - 1
                Dim prName = prNames(i)
                Dim prIsEnabled = prEnabled(i)
                Dim rowId = "pr-" & WebUtility.UrlEncode(prName).Replace("%", "_")
                Dim statusText As String
                Dim badgeClass As String
                Dim actionsHtml As String

                If Not prIsEnabled Then
                    badgeClass = "badge-disabled"
                    statusText = "Disabled"
                    actionsHtml = ""
                ElseIf liveDevices.ContainsKey(prName) Then
                    Dim dev = liveDevices(prName)
                    If dev.Connected Then
                        badgeClass = "badge-connected"
                        statusText = "Connected"
                        actionsHtml = $"<form method='POST' action='action' style='display:inline; margin-left:8px;'><input type='hidden' name='cmd' value='disconnect' /><input type='hidden' name='dev' value='{WebUtility.HtmlEncode(prName)}' /><button type='submit' class='btn btn-secondary'>Stop</button></form>"
                    ElseIf dev.Connecting Then
                        badgeClass = "badge-connecting"
                        statusText = "Connecting..."
                        actionsHtml = $"<form method='POST' action='action' style='display:inline; margin-left:8px;'><input type='hidden' name='cmd' value='disconnect' /><input type='hidden' name='dev' value='{WebUtility.HtmlEncode(prName)}' /><button type='submit' class='btn btn-secondary'>Stop</button></form>"
                    Else
                        badgeClass = "badge-disconnected"
                        statusText = "Disconnected"
                        actionsHtml = $"<form method='POST' action='action' style='display:inline; margin-left:8px;'><input type='hidden' name='cmd' value='connect' /><input type='hidden' name='dev' value='{WebUtility.HtmlEncode(prName)}' /><button type='submit' class='btn btn-primary'>Start</button></form>"
                    End If
                Else
                    badgeClass = "badge-disconnected"
                    statusText = "Stopped"
                    actionsHtml = $"<form method='POST' action='action' style='display:inline; margin-left:8px;'><input type='hidden' name='cmd' value='connect' /><input type='hidden' name='dev' value='{WebUtility.HtmlEncode(prName)}' /><button type='submit' class='btn btn-primary'>Start</button></form>"
                End If

                ' Escape strings for JSON — replace backslash then quote
                Dim jsonName = prName.Replace("\", "\\").Replace("""", "\""")
                Dim jsonRowId = rowId.Replace("\", "\\").Replace("""", "\""")
                Dim jsonStatus = statusText.Replace("\", "\\").Replace("""", "\""")
                Dim jsonBadge = badgeClass.Replace("\", "\\").Replace("""", "\""")
                ' actionsHtml uses single quotes so no JSON escaping needed for inner attributes
                Dim jsonActions = actionsHtml.Replace("\", "\\").Replace("""", "\""")

                Dim comma = If(i < prNames.Count - 1, ",", "")
                sb.AppendLine($"    {{ ""name"": ""{jsonName}"", ""rowId"": ""{jsonRowId}"", ""status"": ""{jsonStatus}"", ""badgeClass"": ""{jsonBadge}"", ""actionsHtml"": ""{jsonActions}"" }}{comma}")
            Next

            sb.AppendLine("  ]")
            sb.Append("}")

            Dim buffer = Encoding.UTF8.GetBytes(sb.ToString())
            context.Response.ContentLength64 = buffer.Length
            context.Response.ContentType = "application/json; charset=utf-8"
            context.Response.Headers.Add("Cache-Control", "no-cache")
            context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        Catch ex As Exception
            _logger.LogError("Error in ServeAdminStatus: {Error}", ex.Message)
            context.Response.StatusCode = 500
        End Try
        context.Response.Close()
    End Sub

    ''' <summary>
    ''' GET /admin/log — returns the last 100 lines of printers.log as plain text.
    ''' Consumed by the admin panel JavaScript every 5 seconds for the log tail viewer.
    ''' </summary>
    Private Sub ServeAdminLog(context As HttpListenerContext)
        Try
            Dim logFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "printers.log")
            Dim text As String

            If File.Exists(logFile) Then
                ' Read tail efficiently — open with shared read so the logger can still write
                Dim lines As New List(Of String)
                Using fs As New FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                    Using sr As New StreamReader(fs, Encoding.UTF8)
                        Dim line As String = sr.ReadLine()
                        While line IsNot Nothing
                            lines.Add(line)
                            line = sr.ReadLine()
                        End While
                    End Using
                End Using
                ' Take last 100 lines
                Dim startLine = Math.Max(0, lines.Count - 100)
                text = String.Join(Environment.NewLine, lines.Skip(startLine))
            Else
                text = "(Log file not found — the engine may not have written any entries yet.)"
            End If

            Dim buffer = Encoding.UTF8.GetBytes(text)
            context.Response.ContentLength64 = buffer.Length
            context.Response.ContentType = "text/plain; charset=utf-8"
            context.Response.Headers.Add("Cache-Control", "no-cache")
            context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        Catch ex As Exception
            _logger.LogError("Error in ServeAdminLog: {Error}", ex.Message)
            context.Response.StatusCode = 500
        End Try
        context.Response.Close()
    End Sub

End Class
