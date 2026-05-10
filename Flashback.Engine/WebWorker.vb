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
        _logger.LogInformation("Flashback Web Server starting on port {Port}.", _port)
        
        _listener = New HttpListener()
        _listener.Prefixes.Add($"http://*:{_port}/")
        
        Try
            _listener.Start()
        Catch ex As Exception
            _logger.LogCritical("Failed to start HttpListener: {Error}", ex.Message)
            Return
        End Try

        While Not stoppingToken.IsCancellationRequested
            Try
                Dim context = Await _listener.GetContextAsync()
                ProcessRequest(context)
            Catch ex As HttpListenerException
                If stoppingToken.IsCancellationRequested Then Exit While
                _logger.LogError("HttpListener error: {Error}", ex.Message)
            Catch ex As Exception
                _logger.LogError("Unexpected web server error: {Error}", ex.Message)
            End Try
        End While

        _listener.Stop()
        _logger.LogInformation("Flashback Web Server stopped.")
    End Function

    Private Sub ProcessRequest(context As HttpListenerContext)
        Task.Run(Sub()
            Try
                ' Basic Auth Check
                Dim authHeader = context.Request.Headers("Authorization")
                Dim user As UserInfo = Nothing

                If Not String.IsNullOrEmpty(authHeader) AndAlso authHeader.StartsWith("Basic ") Then
                    Dim creds = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Substring(6))).Split(":")
                    If creds.Length = 2 Then
                        user = UserManager.Authenticate(creds(0), creds(1))
                    End If
                End If

                If user Is Nothing Then
                    context.Response.StatusCode = 401
                    context.Response.Headers.Add("WWW-Authenticate", "Basic realm=""Flashback Spool View""")
                    context.Response.Close()
                    Return
                End If

                Dim url = context.Request.Url.LocalPath
                If url = "/" OrElse url = "/index.html" Then
                    Dim printerFilter = context.Request.QueryString("printer")
                    ServeDashboard(context, user, printerFilter)
                ElseIf url.StartsWith("/download/") Then
                    ServeFile(context, user, url.Substring(10))
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

    Private Sub ServeDashboard(context As HttpListenerContext, user As UserInfo, printerFilter As String)
        Dim html = GenerateHtml(user, printerFilter)
        Dim buffer = Encoding.UTF8.GetBytes(html)
        context.Response.ContentLength64 = buffer.Length
        context.Response.ContentType = "text/html; charset=utf-8"
        context.Response.OutputStream.Write(buffer, 0, buffer.Length)
        context.Response.Close()
    End Sub

    Private Sub ServeFile(context As HttpListenerContext, user As UserInfo, encodedPath As String)
        Try
            Dim relPath = WebUtility.UrlDecode(encodedPath).Replace("/", Path.DirectorySeparatorChar)
            
            ' Verify the file is within an allowed directory
            Dim allowedDevices = GetAllowedDevices(user)
            Dim targetFile As String = Nothing
            
            For Each kvp In allowedDevices
                Dim root = kvp.Value
                Dim testPath = Path.Combine(root, relPath)
                If File.Exists(testPath) AndAlso testPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) Then
                    targetFile = testPath
                    Exit For
                End If
            Next

            If targetFile IsNot Nothing Then
                Dim buffer = File.ReadAllBytes(targetFile)
                context.Response.ContentType = "application/pdf"
                context.Response.ContentLength64 = buffer.Length
                context.Response.AddHeader("Content-Disposition", $"inline; filename=""{Path.GetFileName(targetFile)}""")
                context.Response.OutputStream.Write(buffer, 0, buffer.Length)
            Else
                context.Response.StatusCode = 404
            End If
        Catch ex As Exception
            _logger.LogError("Error serving file {Path}: {Error}", encodedPath, ex.Message)
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
                        ' Filter by HomeFolder if set
                        If String.IsNullOrEmpty(user.HomeFolder) OrElse outDir.Contains(user.HomeFolder, StringComparison.OrdinalIgnoreCase) Then
                            If Not devices.ContainsKey(devName) Then devices.Add(devName, outDir)
                        End If
                    End If
                End If
            Next
        End If
        
        Return devices
    End Function

    Private Function GenerateHtml(user As UserInfo, printerFilter As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("<!DOCTYPE html><html lang=""en""><head>")
        sb.AppendLine("<meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">")
        sb.AppendLine("<title>Flashback Spool View</title>")
        sb.AppendLine($"<style>{WebAssets.Css}</style></head><body>")
        sb.AppendLine("<header><div class=""container"">")
        If Not String.IsNullOrEmpty(printerFilter) Then
            sb.AppendLine($"<h1><a href=""/"" style=""text-decoration:none; color:inherit;"">🖨️</a> {printerFilter}</h1>")
        Else
            sb.AppendLine("<h1>🖨️ Flashback Spool View</h1>")
        End If
        sb.AppendLine("</div></header>")
        sb.AppendLine("<main class=""container"">")

        Dim allowedDevices = GetAllowedDevices(user)

        If String.IsNullOrEmpty(printerFilter) Then
            ' Show Printer Selection
            sb.AppendLine("<div class=""section"">")
            sb.AppendLine("<h2 class=""section-title"">Select a Printer</h2>")
            sb.AppendLine("<div class=""file-list"">")
            For Each kvp In allowedDevices
                sb.AppendLine($"<a href=""?printer={WebUtility.UrlEncode(kvp.Key)}"" class=""file-card"">")
                sb.AppendLine("<div style=""font-size: 32px; margin-bottom: 10px;"">📠</div>")
                sb.AppendLine($"<div class=""file-name"">{kvp.Key}</div>")
                sb.AppendLine("<div class=""file-meta"">View spool files</div>")
                sb.AppendLine("</a>")
            Next
            sb.AppendLine("</div></div>")
        Else
            ' Show Files for specific printer
            If allowedDevices.ContainsKey(printerFilter) Then
                Dim root = allowedDevices(printerFilter)
                Dim files = Directory.GetFiles(root, "*.pdf", SearchOption.AllDirectories) _
                            .Select(Function(f) New FileInfo(f)) _
                            .OrderByDescending(Function(f) f.LastWriteTime)
                
                If files.Any() Then
                    sb.AppendLine("<div class=""file-list"">")
                    For Each fi In files
                        Dim relPath = Path.GetRelativePath(root, fi.FullName).Replace(Path.DirectorySeparatorChar, "/"c)
                        Dim url = "/download/" & WebUtility.UrlEncode(relPath)
                        Dim sizeMb = fi.Length / (1024 * 1024)
                        
                        sb.AppendLine($"<a href=""{url}"" class=""file-card"" target=""_blank"">")
                        sb.AppendLine("<div class=""thumbnail-container"">")
                        sb.AppendLine($"<object class=""pdf-preview"" data=""{url}#page=1&toolbar=0&navpanes=0&scrollbar=0"" type=""application/pdf""></object>")
                        sb.AppendLine($"<div class=""file-icon-overlay"">{WebAssets.FileIconSvg}</div>")
                        sb.AppendLine("</div>")
                        sb.AppendLine($"<div class=""file-name"" title=""{fi.Name}"">{fi.Name}</div>")
                        sb.AppendLine($"<div class=""file-meta"">{sizeMb:F2} MB • {fi.LastWriteTime:yyyy-MM-dd HH:mm}</div>")
                        sb.AppendLine("</a>")
                    Next
                    sb.AppendLine("</div>")
                Else
                    sb.AppendLine("<div class=""empty-state"">No spool files found for this printer.</div>")
                End If
            Else
                sb.AppendLine("<div class=""empty-state"">Printer not found or access denied.</div>")
            End If
        End If

        sb.AppendLine("</main></body></html>")
        Return sb.ToString()
    End Function
End Class
