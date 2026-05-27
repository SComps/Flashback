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
    Private ReadOnly _port As Integer
    Private _listener As HttpListener

    Public Sub New(logger As ILogger(Of WebWorker))
        _logger = logger
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
                    _logger.LogDebug("Incoming request: {Method} {Url}", context.Request.HttpMethod, context.Request.Url)
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
        Task.Run(Sub()
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

                _logger.LogInformation("Request: {Method} {Url} (Printer: {Printer}, SubUser: {SubUser}) -> RequiresAuth: {Req}", context.Request.HttpMethod, url, printerFilter, userFilter, requiresAuth)

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

                If url = "/" OrElse url = "/index.html" Then
                    ServeDashboard(context, user, printerFilter, userFilter)
                ElseIf url = "/email" Then
                    If context.Request.HttpMethod = "GET" Then
                        ServeEmailForm(context, printerFilter, userFilter, fileParam)
                    ElseIf context.Request.HttpMethod = "POST" Then
                        HandleEmailSubmit(context, printerFilter, userFilter, fileParam)
                    Else
                        context.Response.StatusCode = 405
                        context.Response.Close()
                    End If
                ElseIf isDirectDownload Then
                    Dim printerName = WebUtility.UrlDecode(parts(0))
                    Dim subFolder = WebUtility.UrlDecode(parts(1))
                    Dim fileName = WebUtility.UrlDecode(String.Join("/", parts.Skip(2)))
                    
                    Dim allowedDevices = GetAllowedDevices(Nothing)
                    If allowedDevices.ContainsKey(printerName) Then
                        Dim root = allowedDevices(printerName)
                        Dim filePath = Path.Combine(root, subFolder, fileName)
                        ServeFile(context, filePath)
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
        End Sub)
    End Sub

    Private Sub ServeDashboard(context As HttpListenerContext, user As UserInfo, printerFilter As String, userFilter As String)
        Dim html = GenerateHtml(user, printerFilter, userFilter)
        Dim buffer = Encoding.UTF8.GetBytes(html)
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub

    Private Sub ServeFile(context As HttpListenerContext, filePath As String)
        Try
            filePath = Path.GetFullPath(filePath)
            
            ' Security: verify the file is within an allowed device output directory
            Dim allowedDevices = GetAllowedDevices(Nothing)
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
        sb.AppendLine("<title>Flashback Spool View</title>")
        sb.AppendLine($"<style>{WebAssets.Css}</style></head><body>")
        sb.AppendLine("<header><div class=""container"">")
        
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy/MM/dd")
        
        If Not String.IsNullOrEmpty(userFilter) Then
            sb.AppendLine($"<h1>FLASHBACK SPOOL MANAGEMENT - USER: {WebUtility.HtmlEncode(userFilter).ToUpper()}</h1>")
        ElseIf Not String.IsNullOrEmpty(printerFilter) Then
            sb.AppendLine($"<h1>FLASHBACK SPOOL MANAGEMENT - PRINTER: {WebUtility.HtmlEncode(printerFilter).ToUpper()}</h1>")
        Else
            sb.AppendLine("<h1>FLASHBACK SPOOL MANAGEMENT SYSTEM</h1>")
        End If
        
        sb.AppendLine($"<div class=""system-info"">DATE: {currentDate}  TIME: {currentTime}  USER: {If(user IsNot Nothing, user.Username.ToUpper(), "GUEST")}  TERMINAL: WEB</div>")
        sb.AppendLine("</div></header>")
        sb.AppendLine("<main class=""container"">")

        Dim allowedDevices = GetAllowedDevices(user)

        If String.IsNullOrEmpty(printerFilter) Then
            ' Level 1: List Printers (Public)
            sb.AppendLine("<div class=""section"">")
            sb.AppendLine("<h2 class=""section-title"">Available Printers</h2>")
            sb.AppendLine("<div class=""file-list"">")
            For Each kvp In allowedDevices
                sb.AppendLine($"<a href=""?printer={WebUtility.UrlEncode(kvp.Key)}"" class=""file-card printer-icon"">")
                sb.AppendLine($"<div class=""file-name"">{WebUtility.HtmlEncode(kvp.Key)}</div>")
                sb.AppendLine("<div class=""file-meta"">READY | ONLINE</div>")
                sb.AppendLine("</a>")
            Next
            sb.AppendLine("</div></div>")
        ElseIf String.IsNullOrEmpty(userFilter) Then
            ' Level 2: List User Folders within Printer (Public)
            If allowedDevices.ContainsKey(printerFilter) Then
                Dim root = allowedDevices(printerFilter)
                Dim subDirs = Directory.GetDirectories(root)
                
                sb.AppendLine("<div class=""section"">")
                sb.AppendLine($"<h2 class=""section-title"">User Folders for {WebUtility.HtmlEncode(printerFilter)}</h2>")
                sb.AppendLine("<div class=""file-list"">")
                
                For Each subDir In subDirs
                    Dim dirName = Path.GetFileName(subDir)
                    ' Check if this directory corresponds to a protected web user
                    Dim domainDirName = $"{printerFilter}\{dirName}"
                    Dim isProtected = UserManager.GetUsers().Any(Function(u) u.Username.Equals(dirName, StringComparison.OrdinalIgnoreCase) OrElse u.Username.Equals(domainDirName, StringComparison.OrdinalIgnoreCase))
                    
                    sb.AppendLine($"<a href=""?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(dirName)}"" class=""file-card"">")
                    If isProtected Then
                        sb.AppendLine("<div class=""badge-locked"" style=""position:absolute; top:8px; right:8px;"">🔒 Protected</div>")
                    End If
                    sb.AppendLine("<div style=""font-size: 32px; margin-bottom: 10px;"">📁</div>")
                    sb.AppendLine($"<div class=""file-name"">{WebUtility.HtmlEncode(dirName)}</div>")
                    sb.AppendLine("<div class=""file-meta"">View documents</div>")
                    sb.AppendLine("</a>")
                Next
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
                    
                    If files.Any() Then
                        sb.AppendLine("<div class=""file-list"">")
                        For Each fi In files
                            ' Generate a clean, direct, relative URL that won't trip up nginx/WAF
                            Dim downloadUrl = $"{WebUtility.UrlEncode(printerFilter)}/{WebUtility.UrlEncode(userFilter)}/{WebUtility.UrlEncode(fi.Name)}"
                            Dim emailUrl = $"/email?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(userFilter)}&file={WebUtility.UrlEncode(fi.Name)}"
                            Dim sizeMb = fi.Length / (1024 * 1024)
                            
                            sb.AppendLine($"<div class=""file-card"">")
                            sb.AppendLine($"<a href=""{downloadUrl}"" target=""_blank"" style=""flex: 1;"">")
                            sb.AppendLine($"<span class=""file-name"">{WebUtility.HtmlEncode(fi.Name).PadRight(40)}</span>")
                            sb.AppendLine($"<span class=""file-meta"">{sizeMb:F2} MB  {fi.LastWriteTime:yyyy-MM-dd HH:mm}</span>")
                            sb.AppendLine("</a>")
                            sb.AppendLine($"<a href=""{emailUrl}"" class=""email-link"" title=""Email this file"">[ EMAIL ]</a>")
                            sb.AppendLine("</div>")
                        Next
                        sb.AppendLine("</div>")
                    Else
                        sb.AppendLine("<div class=""empty-state"">No spool files found for this user.</div>")
                    End If
                End If
            End If
        End If

        sb.AppendLine("</main></body></html>")
        Return sb.ToString()
    End Function

    Private Sub ServeEmailForm(context As HttpListenerContext, printerFilter As String, userFilter As String, fileName As String)
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html><html lang=""en""><head>")
        sb.AppendLine("<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        sb.AppendLine("<title>Email PDF - Flashback</title>")
        sb.AppendLine($"<style>{WebAssets.Css}</style></head><body>")
        sb.AppendLine("<header><div class=""container"">")
        sb.AppendLine("<h1>EMAIL PDF DOCUMENT</h1>")
        
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy/MM/dd")
        sb.AppendLine($"<div class=""system-info"">DATE: {currentDate}  TIME: {currentTime}  TERMINAL: WEB</div>")
        sb.AppendLine("</div></header>")
        sb.AppendLine("<main class=""container"">")
        sb.AppendLine("<div class=""section"">")
        sb.AppendLine($"<h2 class=""section-title"">SEND FILE VIA EMAIL</h2>")
        sb.AppendLine($"<p style=""color: #ffffff; margin-bottom: 20px;"">FILE: {WebUtility.HtmlEncode(fileName)}</p>")
        
        sb.AppendLine($"<form method=""POST"" action=""/email?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(userFilter)}&file={WebUtility.UrlEncode(fileName)}"" style=""max-width: 600px;"">")
        sb.AppendLine("<div style=""margin-bottom: 20px;"">")
        sb.AppendLine("<label for=""email"" style=""display: block; color: #00ffff; margin-bottom: 8px; font-weight: 600;"">RECIPIENT EMAIL ADDRESS:</label>")
        sb.AppendLine("<input type=""email"" id=""email"" name=""email"" required style=""width: 100%; padding: 10px; font-family: 'IBM Plex Mono', monospace; font-size: 0.95rem; background-color: #000080; color: #ffffff; border: 1px solid #00ffff; outline: none;"" />")
        sb.AppendLine("</div>")
        
        sb.AppendLine("<div style=""margin-bottom: 20px;"">")
        sb.AppendLine("<label for=""subject"" style=""display: block; color: #00ffff; margin-bottom: 8px; font-weight: 600;"">SUBJECT (OPTIONAL):</label>")
        sb.AppendLine($"<input type=""text"" id=""subject"" name=""subject"" value=""Flashback Spool: {WebUtility.HtmlEncode(fileName)}"" style=""width: 100%; padding: 10px; font-family: 'IBM Plex Mono', monospace; font-size: 0.95rem; background-color: #000080; color: #ffffff; border: 1px solid #00ffff; outline: none;"" />")
        sb.AppendLine("</div>")
        
        sb.AppendLine("<div style=""margin-bottom: 20px;"">")
        sb.AppendLine("<label for=""message"" style=""display: block; color: #00ffff; margin-bottom: 8px; font-weight: 600;"">MESSAGE (OPTIONAL):</label>")
        sb.AppendLine("<textarea id=""message"" name=""message"" rows=""4"" style=""width: 100%; padding: 10px; font-family: 'IBM Plex Mono', monospace; font-size: 0.95rem; background-color: #000080; color: #ffffff; border: 1px solid #00ffff; outline: none; resize: vertical;"">Please find the attached PDF document from the Flashback spool system.</textarea>")
        sb.AppendLine("</div>")
        
        sb.AppendLine("<div style=""display: flex; gap: 15px;"">")
        sb.AppendLine("<button type=""submit"" style=""padding: 12px 24px; font-family: 'IBM Plex Mono', monospace; font-size: 0.95rem; font-weight: 600; background-color: #00ffff; color: #000080; border: none; cursor: pointer; text-transform: uppercase;"">[ SEND EMAIL ]</button>")
        sb.AppendLine($"<a href=""/?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(userFilter)}"" style=""padding: 12px 24px; font-family: 'IBM Plex Mono', monospace; font-size: 0.95rem; font-weight: 600; background-color: transparent; color: #00ffff; border: 1px solid #00ffff; text-decoration: none; display: inline-block; text-transform: uppercase;"">[ CANCEL ]</a>")
        sb.AppendLine("</div>")
        sb.AppendLine("</form>")
        sb.AppendLine("</div></main>")
        sb.AppendLine("<div class=""status-bar"">FLASHBACK v1.0</div>")
        sb.AppendLine("</body></html>")
        
        Dim buffer = Encoding.UTF8.GetBytes(sb.ToString())
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub

    Private Sub HandleEmailSubmit(context As HttpListenerContext, printerFilter As String, userFilter As String, fileName As String)
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
                            .SmtpServer = If(p.Length > 14, p(14), ""),
                            .SmtpPort = If(p.Length > 15 AndAlso Integer.TryParse(p(15), Nothing), CInt(p(15)), 587),
                            .SmtpUsername = If(p.Length > 16, p(16), ""),
                            .SmtpPassword = If(p.Length > 17, p(17), ""),
                            .SmtpUseTLS = If(p.Length > 18, p(18).Equals("true", StringComparison.OrdinalIgnoreCase), True),
                            .EmailFromAddress = If(p.Length > 19, p(19), ""),
                            .EmailFromName = If(p.Length > 20, p(20), "Flashback Spool System")
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
            Dim success = emailService.SendPdfEmailAsync(emailConfig, filePath, fileName, device.DevName, userFilter, 0).Result
            
            If success Then
                ServeSuccessPage(context, recipientEmail, printerFilter, userFilter)
            Else
                ServeErrorPage(context, "Failed to send email. Please check the logs for details.")
            End If
            
        Catch ex As Exception
            _logger.LogError("Error sending email: {Error}", ex.Message)
            ServeErrorPage(context, $"Error: {ex.Message}")
        End Try
    End Sub

    Private Sub ServeSuccessPage(context As HttpListenerContext, email As String, printerFilter As String, userFilter As String)
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html><html lang=""en""><head>")
        sb.AppendLine("<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        sb.AppendLine("<title>Email Sent - Flashback</title>")
        sb.AppendLine($"<style>{WebAssets.Css}</style></head><body>")
        sb.AppendLine("<header><div class=""container"">")
        sb.AppendLine("<h1>EMAIL SENT SUCCESSFULLY</h1>")
        
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy/MM/dd")
        sb.AppendLine($"<div class=""system-info"">DATE: {currentDate}  TIME: {currentTime}  TERMINAL: WEB</div>")
        sb.AppendLine("</div></header>")
        sb.AppendLine("<main class=""container"">")
        sb.AppendLine("<div class=""section"">")
        sb.AppendLine($"<p style=""color: #00ffff; font-size: 1.1rem; margin-bottom: 20px;"">*** EMAIL DELIVERED TO: {WebUtility.HtmlEncode(email)} ***</p>")
        sb.AppendLine($"<p style=""color: #ffffff; margin-bottom: 30px;"">The PDF document has been successfully sent to the specified email address.</p>")
        sb.AppendLine($"<a href=""/?printer={WebUtility.UrlEncode(printerFilter)}&subuser={WebUtility.UrlEncode(userFilter)}"" style=""padding: 12px 24px; font-family: 'IBM Plex Mono', monospace; font-size: 0.95rem; font-weight: 600; background-color: #00ffff; color: #000080; text-decoration: none; display: inline-block; text-transform: uppercase;"">[ RETURN TO FILE LIST ]</a>")
        sb.AppendLine("</div></main>")
        sb.AppendLine("<div class=""status-bar"">FLASHBACK v1.0</div>")
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
        sb.AppendLine("<header><div class=""container"">")
        sb.AppendLine("<h1>ERROR</h1>")
        
        Dim currentTime = DateTime.Now.ToString("HH:mm:ss")
        Dim currentDate = DateTime.Now.ToString("yyyy/MM/dd")
        sb.AppendLine($"<div class=""system-info"">DATE: {currentDate}  TIME: {currentTime}  TERMINAL: WEB</div>")
        sb.AppendLine("</div></header>")
        sb.AppendLine("<main class=""container"">")
        sb.AppendLine("<div class=""section"">")
        sb.AppendLine($"<p style=""color: #ffff00; font-size: 1.1rem; margin-bottom: 20px;"">*** ERROR: {WebUtility.HtmlEncode(errorMessage)} ***</p>")
        sb.AppendLine($"<a href=""javascript:history.back()"" style=""padding: 12px 24px; font-family: 'IBM Plex Mono', monospace; font-size: 0.95rem; font-weight: 600; background-color: #00ffff; color: #000080; text-decoration: none; display: inline-block; text-transform: uppercase;"">[ GO BACK ]</a>")
        sb.AppendLine("</div></main>")
        sb.AppendLine("<div class=""status-bar"">FLASHBACK v1.0</div>")
        sb.AppendLine("</body></html>")
        
        Dim buffer = Encoding.UTF8.GetBytes(sb.ToString())
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub
End Class
