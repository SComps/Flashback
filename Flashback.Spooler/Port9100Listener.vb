Imports System
Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Threading
Imports Flashback.Spooler.Models
Imports Microsoft.Extensions.Logging

''' <summary>
''' Listens on port 9100 for incoming print jobs (JetDirect compatible)
''' Receives raw print data and stores it to spool files
''' </summary>
Public Class Port9100Listener
    Private ReadOnly _logger As ILogger
    Private ReadOnly _config As BehaviorConfig
    Private ReadOnly _spoolManager As SpoolManager
    Private ReadOnly _jobQueue As JobQueue
    Private _listener As TcpListener
    Private _cancellationTokenSource As CancellationTokenSource
    Private _isRunning As Boolean = False

    Public Sub New(logger As ILogger, config As BehaviorConfig, spoolManager As SpoolManager, jobQueue As JobQueue)
        _logger = logger
        _config = config
        _spoolManager = spoolManager
        _jobQueue = jobQueue
    End Sub

    ''' <summary>
    ''' Starts the port 9100 listener
    ''' </summary>
    Public Async Function StartAsync(cancellationToken As CancellationToken) As Task
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        
        Try
            _listener = New TcpListener(IPAddress.Any, 9100)
            _listener.Start()
            _isRunning = True
            
            _logger.LogInformation("Port 9100 listener started. Waiting for print jobs...")
            
            ' Accept connections continuously
            Using registration = _cancellationTokenSource.Token.Register(Sub() _listener.Stop())
                While Not _cancellationTokenSource.Token.IsCancellationRequested
                    Try
                        Dim client = Await _listener.AcceptTcpClientAsync()
                        _logger.LogInformation("Accepted connection from {RemoteEndpoint}", client.Client.RemoteEndPoint)
                        
                        ' Handle each client connection in a separate task
                        Dim clientTask = HandleClientAsync(client, _cancellationTokenSource.Token)
                        
                    Catch ex As ObjectDisposedException
                        ' Listener was stopped
                        Exit While
                    Catch ex As Exception
                        If Not _cancellationTokenSource.Token.IsCancellationRequested Then
                            _logger.LogError(ex, "Error accepting client connection")
                        End If
                    End Try
                End While
            End Using
            
        Catch ex As Exception
            _logger.LogError(ex, "Port 9100 listener failed to start")
            Throw
        Finally
            _isRunning = False
            _listener?.Stop()
            _logger.LogInformation("Port 9100 listener stopped.")
        End Try
    End Function

    ''' <summary>
    ''' Handles an individual client connection
    ''' </summary>
    Private Async Function HandleClientAsync(client As TcpClient, cancellationToken As CancellationToken) As Task
        Dim job As PrintJob = Nothing
        Dim spoolFilePath As String = Nothing
        Dim sourceEndpoint = client.Client.RemoteEndPoint.ToString()
        
        Try
            ' Create new job and spool file
            job = New PrintJob() With {
                .SourceEndpoint = sourceEndpoint,
                .State = JobState.Receiving
            }
            
            spoolFilePath = _spoolManager.CreateSpoolFile()
            job.SpoolFilePath = spoolFilePath
            
            _logger.LogInformation("Job {JobId} receiving from {Source}", job.JobId, sourceEndpoint)
            
            ' Receive data from client
            Using stream = client.GetStream()
                Dim buffer(8192 - 1) As Byte
                Dim totalBytes As Long = 0
                Dim lastReceivedTime = DateTime.Now
                Dim maxJobSizeBytes As Long = CLng(_config.MaxJobSizeMB) * 1024 * 1024
                
                While Not cancellationToken.IsCancellationRequested
                    ' Check for data availability with timeout
                    If Not stream.DataAvailable Then
                        Await Task.Delay(100, cancellationToken)
                        
                        ' Check for inactivity timeout
                        Dim inactivitySeconds = (DateTime.Now - lastReceivedTime).TotalSeconds
                        If inactivitySeconds > _config.JobCompletionTimeout Then
                            _logger.LogDebug("Job {JobId} inactivity timeout ({Timeout}s). Job complete.", 
                                           job.JobId, _config.JobCompletionTimeout)
                            Exit While
                        End If
                        
                        Continue While
                    End If
                    
                    ' Read data
                    Dim bytesRead = Await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    
                    If bytesRead = 0 Then
                        ' Client closed connection
                        _logger.LogDebug("Job {JobId} connection closed by client. Job complete.", job.JobId)
                        Exit While
                    End If
                    
                    ' Write to spool file
                    Await _spoolManager.WriteToSpoolFileAsync(spoolFilePath, buffer.Take(bytesRead).ToArray(), cancellationToken)
                    
                    totalBytes += bytesRead
                    lastReceivedTime = DateTime.Now
                    
                    ' Check size limit
                    If _config.MaxJobSizeMB > 0 AndAlso totalBytes > maxJobSizeBytes Then
                        _logger.LogWarning("Job {JobId} exceeded size limit ({MaxSize}MB). Truncating.", 
                                         job.JobId, _config.MaxJobSizeMB)
                        Exit While
                    End If
                End While
                
                ' Job complete
                job.FileSize = totalBytes
                job.State = JobState.Spooled
                
                _logger.LogInformation("Job {JobId} received successfully. Size: {Size} bytes", 
                                     job.JobId, totalBytes)
                
                ' Add to queue for transmission
                _jobQueue.Enqueue(job)
            End Using
            
        Catch ex As OperationCanceledException
            _logger.LogInformation("Job {JobId} reception cancelled.", job?.JobId)
            
            ' Clean up partial spool file
            If spoolFilePath IsNot Nothing Then
                _spoolManager.DeleteSpoolFile(spoolFilePath)
            End If
            
        Catch ex As Exception
            _logger.LogError(ex, "Error handling client connection from {Source}", sourceEndpoint)
            
            ' Clean up partial spool file
            If spoolFilePath IsNot Nothing Then
                _spoolManager.DeleteSpoolFile(spoolFilePath)
            End If
            
        Finally
            Try
                client?.Close()
            Catch
                ' Ignore close errors
            End Try
        End Try
    End Function

    ''' <summary>
    ''' Stops the port 9100 listener
    ''' </summary>
    Public Sub [Stop]()
        _logger.LogInformation("Stopping port 9100 listener...")
        _cancellationTokenSource?.Cancel()
        _listener?.Stop()
    End Sub

    ''' <summary>
    ''' Gets whether the listener is currently running
    ''' </summary>
    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _isRunning
        End Get
    End Property
End Class

' Made with Bob
