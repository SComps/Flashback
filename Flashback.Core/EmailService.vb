Imports System.IO
Imports System.Threading.Tasks
Imports MailKit.Net.Smtp
Imports MailKit.Security
Imports MimeKit
Imports Microsoft.Extensions.Logging

Public Class EmailService
    Private ReadOnly _logger As ILogger

    Public Sub New(Optional logger As ILogger = Nothing)
        _logger = logger
    End Sub

    ''' <summary>
    ''' Sends an email with a PDF attachment
    ''' </summary>
    ''' <param name="config">Email configuration containing SMTP settings and recipients</param>
    ''' <param name="pdfPath">Full path to the PDF file to attach</param>
    ''' <param name="jobName">Name of the print job</param>
    ''' <param name="deviceName">Name of the device that generated the PDF</param>
    ''' <param name="userName">User who submitted the job</param>
    ''' <param name="pageCount">Number of pages in the PDF</param>
    ''' <returns>True if email sent successfully, False otherwise</returns>
    Public Async Function SendPdfEmailAsync(config As EmailConfig, pdfPath As String, jobName As String, 
                                           deviceName As String, userName As String, pageCount As Integer) As Task(Of Boolean)
        Try
            ' Validate configuration
            If Not ValidateConfig(config) Then
                _logger?.LogError("Email configuration validation failed for device {Device}", deviceName)
                Return False
            End If

            ' Validate PDF file exists
            If Not File.Exists(pdfPath) Then
                _logger?.LogError("PDF file not found: {Path}", pdfPath)
                Return False
            End If

            ' Create email message
            Dim message As New MimeMessage()
            message.From.Add(New MailboxAddress(config.FromName, config.FromAddress))

            ' Add recipients
            For Each recipient In config.Recipients
                If Not String.IsNullOrWhiteSpace(recipient) Then
                    Try
                        message.To.Add(MailboxAddress.Parse(recipient.Trim()))
                    Catch ex As Exception
                        _logger?.LogWarning("Invalid email address skipped: {Email}", recipient)
                    End Try
                End If
            Next

            If message.To.Count = 0 Then
                _logger?.LogError("No valid recipients for email from device {Device}", deviceName)
                Return False
            End If

            ' Set subject with variable substitution
            message.Subject = SubstituteVariables(config.Subject, jobName, deviceName, userName, pageCount)

            ' Create message body
            Dim builder As New BodyBuilder()
            builder.TextBody = SubstituteVariables(config.Body, jobName, deviceName, userName, pageCount)

            ' Attach PDF
            Dim attachment = builder.Attachments.Add(pdfPath)
            attachment.ContentDisposition = New ContentDisposition(ContentDisposition.Attachment)

            message.Body = builder.ToMessageBody()

            ' Send email
            Using client As New SmtpClient()
                ' Configure SSL/TLS
                Dim secureSocketOptions As SecureSocketOptions = If(config.UseTLS, 
                    SecureSocketOptions.StartTls, 
                    SecureSocketOptions.Auto)

                _logger?.LogInformation("Connecting to SMTP server {Server}:{Port} for device {Device}", 
                                       config.SmtpServer, config.SmtpPort, deviceName)

                Await client.ConnectAsync(config.SmtpServer, config.SmtpPort, secureSocketOptions)

                ' Authenticate if credentials provided
                If Not String.IsNullOrWhiteSpace(config.SmtpUsername) Then
                    Await client.AuthenticateAsync(config.SmtpUsername, config.SmtpPassword)
                End If

                ' Send message
                Await client.SendAsync(message)
                Await client.DisconnectAsync(True)

                _logger?.LogInformation("Email sent successfully from device {Device} to {Count} recipient(s)", 
                                       deviceName, message.To.Count)
                Return True
            End Using

        Catch ex As Exception
            _logger?.LogError("Failed to send email from device {Device}: {Error}", deviceName, ex.Message)
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Validates email configuration
    ''' </summary>
    Private Function ValidateConfig(config As EmailConfig) As Boolean
        If config Is Nothing Then Return False
        If String.IsNullOrWhiteSpace(config.SmtpServer) Then Return False
        If config.SmtpPort <= 0 OrElse config.SmtpPort > 65535 Then Return False
        If config.Recipients Is Nothing OrElse config.Recipients.Count = 0 Then Return False
        If String.IsNullOrWhiteSpace(config.FromAddress) Then Return False
        Return True
    End Function

    ''' <summary>
    ''' Substitutes variables in email subject/body templates
    ''' </summary>
    Private Function SubstituteVariables(template As String, jobName As String, deviceName As String, 
                                        userName As String, pageCount As Integer) As String
        If String.IsNullOrWhiteSpace(template) Then Return ""
        
        Dim result = template
        result = result.Replace("{JobName}", jobName)
        result = result.Replace("{DeviceName}", deviceName)
        result = result.Replace("{UserName}", userName)
        result = result.Replace("{PageCount}", pageCount.ToString())
        result = result.Replace("{DateTime}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
        result = result.Replace("{Date}", DateTime.Now.ToString("yyyy-MM-dd"))
        result = result.Replace("{Time}", DateTime.Now.ToString("HH:mm:ss"))
        
        Return result
    End Function
End Class

''' <summary>
''' Email configuration for a device
''' </summary>
Public Class EmailConfig
    Public Property Enabled As Boolean = False
    Public Property Recipients As List(Of String) = New List(Of String)()
    Public Property SmtpServer As String = ""
    Public Property SmtpPort As Integer = 587
    Public Property SmtpUsername As String = ""
    Public Property SmtpPassword As String = ""
    Public Property UseTLS As Boolean = True
    Public Property FromAddress As String = "flashback@localhost"
    Public Property FromName As String = "Flashback Print Server"
    Public Property Subject As String = "Print Job: {JobName} from {DeviceName}"
    Public Property Body As String = "Print job '{JobName}' from device '{DeviceName}' has been completed." & vbCrLf & vbCrLf & 
                                     "User: {UserName}" & vbCrLf & 
                                     "Pages: {PageCount}" & vbCrLf & 
                                     "Date/Time: {DateTime}" & vbCrLf & vbCrLf & 
                                     "The PDF output is attached to this email."

    ''' <summary>
    ''' Parses recipients from comma-separated string
    ''' </summary>
    Public Sub SetRecipientsFromString(recipientString As String)
        Recipients.Clear()
        If Not String.IsNullOrWhiteSpace(recipientString) Then
            For Each email In recipientString.Split(","c, ";"c)
                Dim trimmed = email.Trim()
                If Not String.IsNullOrWhiteSpace(trimmed) Then
                    Recipients.Add(trimmed)
                End If
            Next
        End If
    End Sub

    ''' <summary>
    ''' Gets recipients as comma-separated string
    ''' </summary>
    Public Function GetRecipientsAsString() As String
        Return String.Join(", ", Recipients)
    End Function
End Class
