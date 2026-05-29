Imports System
Imports System.IO
Imports System.Net
Imports System.Net.Sockets
Imports System.Threading
Imports Flashback.Spooler.Models
Imports Microsoft.Extensions.Logging
Public Class EngineListener
    Private ReadOnly _logger As ILogger
    Private ReadOnly _config As ListenerConfig
    Private ReadOnly _spoolManager As SpoolManager
    Private ReadOnly _jobQueue As JobQueue
    Private _listener As TcpListener
    Private _engineClient As TcpClient
    Private _engineStream As NetworkStream
    Private _cancellationTokenSource As CancellationTokenSource
    Private _isRunning As Boolean = False
    Private _isEngineConnected As Boolean = False

    Public Sub New(logger As ILogger, config As ListenerConfig, spoolManager As SpoolManager, jobQueue As JobQueue)
        _logger = logger
        _config = config
        _spoolManager = spoolManager
        _jobQueue = jobQueue
    End Sub
    Public Async Function StartAsync(cancellationToken As CancellationToken) As Task
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        
        Try
            _listener = New TcpListener(IPAddress.Any, _config.EnginePort)
            _listener.Start()
            _isRunning = True
            
            _logger.LogInformation("Engine listener started on port {Port}. Waiting for Flashback.Engine connection...", 
                                 _config.EnginePort)
            
            ' Accept Engine connection
            Using registration = _cancellationTokenSource.Token.Register(Sub() _listener.Stop())
                While Not _cancellationTokenSource.Token.IsCancellationRequested
                    Try
                        ' Wait for Engine to connect
                        _engineClient = Await _listener.AcceptTcpClientAsync()
                        _engineStream = _engineClient.GetStream()
                        _isEngineConnected = True
                        _jobQueue.IsEngineConnected = True
                        
                        _logger.LogInformation("Flashback.Engine connected from {RemoteEndpoint}", 
                                             _engineClient.Client.RemoteEndPoint)
                        
                        ' Process jobs while Engine is connected
                        Await ProcessJobsAsync(_cancellationTokenSource.Token)
                        
                    Catch ex As ObjectDisposedException
                        ' Listener was stopped
                        Exit While
                    Catch ex As Exception
                        If Not _cancellationTokenSource.Token.IsCancellationRequested Then
                            _logger.LogError(ex, "Error with Engine connection")
                        End If
                    Finally
                        ' Clean up connection
                        _isEngineConnected = False
                        _jobQueue.IsEngineConnected = False
                        
                        Try
                            _engineStream?.Close()
                            _engineClient?.Close()
                        Catch
                            ' Ignore close errors
                        End Try
                        
                        _engineStream = Nothing
                        _engineClient = Nothing
                        
                        If Not _cancellationTokenSource.Token.IsCancellationRequested Then
                            _logger.LogWarning("Engine disconnected. Waiting for reconnection...")
                        End If
                    End Try
                End While
            End Using
            
        Catch ex As Exception
            _logger.LogError(ex, "Engine listener failed to start")
            Throw
        Finally
            _isRunning = False
            _listener?.Stop()
            _logger.LogInformation("Engine listener stopped.")
        End Try
    End Function
    Private Async Function ProcessJobsAsync(cancellationToken As CancellationToken) As Task
        Try
            While Not cancellationToken.IsCancellationRequested AndAlso _isEngineConnected
                ' Check if there are jobs in the queue
                Dim job As PrintJob = Nothing
                If Not _jobQueue.TryDequeue(job) Then
                    ' No jobs available, wait a bit
                    Await Task.Delay(500, cancellationToken)
                    Continue While
                End If
                
                ' Transmit the job
                Dim success = Await TransmitJobAsync(job, cancellationToken)
                
                If success Then
                    ' Mark job as completed and delete spool file
                    _jobQueue.MarkCompleted(job)
                    _spoolManager.DeleteSpoolFile(job.SpoolFilePath)
                Else
                    ' Mark job as failed (will retry if configured)
                    _jobQueue.MarkFailed(job)
                End If
            End While
            
        Catch ex As OperationCanceledException
            _logger.LogInformation("Job processing cancelled.")
        Catch ex As Exception
            _logger.LogError(ex, "Error processing jobs")
        End Try
    End Function
    Private Async Function TransmitJobAsync(job As PrintJob, cancellationToken As CancellationToken) As Task(Of Boolean)
        If job Is Nothing Then Return False
        
        Try
            job.State = JobState.Transmitting
            _logger.LogInformation("Transmitting job {JobId} to Engine ({Size} bytes)", 
                                 job.JobId, job.FileSize)
            
            ' Open spool file for reading
            Using fileStream = _spoolManager.OpenSpoolFileForRead(job.SpoolFilePath)
                Dim buffer(8192 - 1) As Byte
                Dim totalBytesSent As Long = 0
                
                While Not cancellationToken.IsCancellationRequested
                    Dim bytesRead = Await fileStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                    
                    If bytesRead = 0 Then
                        ' End of file
                        Exit While
                    End If
                    
                    ' Send to Engine
                    Await _engineStream.WriteAsync(buffer, 0, bytesRead, cancellationToken)
                    totalBytesSent += bytesRead
                End While
                
                ' Flush to ensure all data is sent
                Await _engineStream.FlushAsync(cancellationToken)
                
                _logger.LogInformation("Job {JobId} transmitted successfully ({Size} bytes)", 
                                     job.JobId, totalBytesSent)
                
                Return True
            End Using
            
        Catch ex As IOException
            _logger.LogError(ex, "I/O error transmitting job {JobId}. Engine may have disconnected.", job.JobId)
            _isEngineConnected = False
            Return False
            
        Catch ex As OperationCanceledException
            _logger.LogInformation("Job {JobId} transmission cancelled.", job.JobId)
            Return False
            
        Catch ex As Exception
            _logger.LogError(ex, "Error transmitting job {JobId}", job.JobId)
            Return False
        End Try
    End Function
    Public Sub [Stop]()
        _logger.LogInformation("Stopping Engine listener...")
        _cancellationTokenSource?.Cancel()
        
        Try
            _engineStream?.Close()
            _engineClient?.Close()
        Catch
            ' Ignore close errors
        End Try
        
        _listener?.Stop()
    End Sub
    Public ReadOnly Property IsRunning As Boolean
        Get
            Return _isRunning
        End Get
    End Property
    Public ReadOnly Property IsEngineConnected As Boolean
        Get
            Return _isEngineConnected
        End Get
    End Property
End Class
