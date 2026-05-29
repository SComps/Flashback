Imports System
Imports System.Threading
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports Flashback.Spooler.Models

''' <summary>
''' Main background service worker for Flashback Spooler
''' Coordinates all components and manages service lifecycle
''' </summary>
Public Class SpoolerWorker
    Inherits BackgroundService

    Private ReadOnly _logger As ILogger(Of SpoolerWorker)
    Private ReadOnly _config As SpoolerConfig
    Private ReadOnly _spoolManager As SpoolManager
    Private ReadOnly _jobQueue As JobQueue
    Private ReadOnly _port9100Listener As Port9100Listener
    Private ReadOnly _engineListener As EngineListener
    Private WithEvents _cleanupTimer As System.Timers.Timer

    Public Sub New(logger As ILogger(Of SpoolerWorker))
        _logger = logger
        
        ' Load configuration
        Dim configManager As New ConfigManager()
        _config = configManager.LoadConfig()
        
        ' Initialize components
        _spoolManager = New SpoolManager(logger, _config.Storage)
        _jobQueue = New JobQueue(logger, _config.Behavior)
        _port9100Listener = New Port9100Listener(logger, _config.Behavior, _spoolManager, _jobQueue)
        _engineListener = New EngineListener(logger, _config.Listener, _spoolManager, _jobQueue)
        
        ' Setup cleanup timer (runs every hour)
        _cleanupTimer = New System.Timers.Timer(3600000) ' 1 hour
        _cleanupTimer.AutoReset = True
    End Sub

    Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
        Dim version = Reflection.Assembly.GetExecutingAssembly().GetName().Version
        _logger.LogInformation("Flashback Spooler Service v{Version} Starting.", version.ToString())
        
        ' Log configuration
        _logger.LogInformation("Configuration:")
        _logger.LogInformation("  Port 9100 Enabled: {Enabled}", _config.Listener.Port9100Enabled)
        _logger.LogInformation("  Engine Port: {Port}", _config.Listener.EnginePort)
        _logger.LogInformation("  Spool Directory: {Dir}", _config.Storage.SpoolDirectory)
        _logger.LogInformation("  Max Job Size: {Size}MB", _config.Behavior.MaxJobSizeMB)
        _logger.LogInformation("  Job Timeout: {Timeout}s", _config.Behavior.JobCompletionTimeout)
        _logger.LogInformation("  Retry Enabled: {Enabled}", _config.Behavior.EnableRetry)
        
        ' Get initial spool statistics
        Dim stats = _spoolManager.GetSpoolStatistics()
        _logger.LogInformation("Spool directory contains {Count} existing files ({Size:N0} bytes)", 
                             stats.FileCount, stats.TotalSize)
        
        ' Start cleanup timer
        _cleanupTimer.Enabled = True
        
        Try
            ' Start listeners in parallel
            Dim listenerTasks As New List(Of Task)
            
            If _config.Listener.Port9100Enabled Then
                listenerTasks.Add(_port9100Listener.StartAsync(stoppingToken))
            Else
                _logger.LogWarning("Port 9100 listener is disabled in configuration.")
            End If
            
            listenerTasks.Add(_engineListener.StartAsync(stoppingToken))
            
            ' Wait for all listeners to complete (or cancellation)
            Await Task.WhenAll(listenerTasks)
            
        Catch ex As OperationCanceledException
            _logger.LogInformation("Service shutdown requested.")
        Catch ex As Exception
            _logger.LogError(ex, "Service error occurred")
            Throw
        Finally
            _logger.LogInformation("Flashback Spooler Service Stopping.")
            Cleanup()
        End Try
    End Function

    Private Sub Cleanup()
        _logger.LogInformation("Cleaning up resources...")
        
        ' Stop timers
        _cleanupTimer?.Stop()
        _cleanupTimer?.Dispose()
        
        ' Stop listeners
        Try
            _port9100Listener?.Stop()
        Catch ex As Exception
            _logger.LogError(ex, "Error stopping port 9100 listener")
        End Try
        
        Try
            _engineListener?.Stop()
        Catch ex As Exception
            _logger.LogError(ex, "Error stopping Engine listener")
        End Try
        
        ' Log final statistics
        Dim stats = _spoolManager.GetSpoolStatistics()
        _logger.LogInformation("Final spool statistics: {Count} files ({Size:N0} bytes)", 
                             stats.FileCount, stats.TotalSize)
        
        Dim queueDepth = _jobQueue.Count
        If queueDepth > 0 Then
            _logger.LogWarning("Service stopped with {Count} jobs still in queue.", queueDepth)
        End If
        
        _logger.LogInformation("Cleanup complete.")
    End Sub

    Private Sub CleanupTimer_Elapsed(sender As Object, e As System.Timers.ElapsedEventArgs) Handles _cleanupTimer.Elapsed
        Try
            _logger.LogInformation("Running scheduled spool cleanup...")
            _spoolManager.CleanupOldSpoolFiles()
            
            ' Log current statistics
            Dim stats = _spoolManager.GetSpoolStatistics()
            _logger.LogInformation("Current spool statistics: {Count} files ({Size:N0} bytes)", 
                                 stats.FileCount, stats.TotalSize)
            
            _logger.LogInformation("Queue depth: {Count} jobs", _jobQueue.Count)
            _logger.LogInformation("Engine connected: {Connected}", _engineListener.IsEngineConnected)
            
        Catch ex As Exception
            _logger.LogError(ex, "Error during scheduled cleanup")
        End Try
    End Sub
End Class

' Made with Bob
