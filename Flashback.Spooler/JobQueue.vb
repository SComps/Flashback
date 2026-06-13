Imports System
Imports System.Collections.Concurrent
Imports System.Threading
Imports Flashback.Spooler.Models
Imports Microsoft.Extensions.Logging
Public Class JobQueue
    Private ReadOnly _queue As New ConcurrentQueue(Of PrintJob)
    Private ReadOnly _logger As ILogger
    Private ReadOnly _config As BehaviorConfig
    Private _isEngineConnected As Boolean = False

    ' Events for job state changes
    Public Event JobAdded(job As PrintJob)
    Public Event JobCompleted(job As PrintJob)
    Public Event JobFailed(job As PrintJob)
    Public Event JobExpired(job As PrintJob)

    Public Sub New(logger As ILogger, config As BehaviorConfig)
        _logger = logger
        _config = config
    End Sub
    Public Sub Enqueue(job As PrintJob)
        If job Is Nothing Then
            Throw New ArgumentNullException(NameOf(job))
        End If

        job.State = JobState.Queued
        _queue.Enqueue(job)
        _logger.LogInformation("Job {JobId} queued. Queue depth: {QueueDepth}", job.JobId, _queue.Count)
        RaiseEvent JobAdded(job)
    End Sub
    Public Function TryDequeue(ByRef job As PrintJob) As Boolean
        Return _queue.TryDequeue(job)
    End Function
    Public Function TryPeek(ByRef job As PrintJob) As Boolean
        Return _queue.TryPeek(job)
    End Function
    Public ReadOnly Property Count As Integer
        Get
            Return _queue.Count
        End Get
    End Property
    Public Property IsEngineConnected As Boolean
        Get
            Return _isEngineConnected
        End Get
        Set(value As Boolean)
            Dim wasConnected = _isEngineConnected
            _isEngineConnected = value
            
            If value AndAlso Not wasConnected Then
                _logger.LogInformation("Engine connected. Queue has {QueueDepth} pending jobs.", _queue.Count)
            ElseIf Not value AndAlso wasConnected Then
                _logger.LogWarning("Engine disconnected. Jobs will queue until reconnection.")
            End If
        End Set
    End Property
    Public Sub MarkCompleted(job As PrintJob)
        If job Is Nothing Then Return
        
        job.State = JobState.Completed
        _logger.LogInformation("Job {JobId} completed successfully.", job.JobId)
        RaiseEvent JobCompleted(job)
    End Sub
    Public Function MarkFailed(job As PrintJob) As Boolean
        If job Is Nothing Then Return False
        
        job.RetryCount += 1
        job.LastAttemptTime = DateTime.Now
        
        If _config.EnableRetry AndAlso job.RetryCount <= _config.MaxRetries Then
            job.State = JobState.Queued
            _logger.LogWarning("Job {JobId} failed (attempt {Attempt}/{MaxAttempts}). Will retry in {Delay} seconds.", 
                             job.JobId, job.RetryCount, _config.MaxRetries, _config.RetryDelaySeconds)
            
            ' Re-queue the job after delay
            Task.Run(Async Function()
                         Await Task.Delay(_config.RetryDelaySeconds * 1000)
                         Enqueue(job)
                     End Function)
            
            RaiseEvent JobFailed(job)
            Return True ' Will retry
        Else
            job.State = JobState.Expired
            _logger.LogError("Job {JobId} exceeded retry limit ({MaxAttempts} attempts). Marking as expired.", 
                           job.JobId, _config.MaxRetries)
            RaiseEvent JobExpired(job)
            Return False ' Will not retry
        End If
    End Function
    Public Function ShouldExpireJob(job As PrintJob, maxAgeHours As Integer) As Boolean
        If job Is Nothing Then Return False
        
        Dim age = DateTime.Now - job.ReceivedTime
        Return age.TotalHours > maxAgeHours
    End Function
    Public Function GetQueueSnapshot() As PrintJob()
        Return _queue.ToArray()
    End Function
    Public Sub Clear()
        While _queue.TryDequeue(Nothing)
            ' Drain the queue
        End While
        _logger.LogWarning("Job queue cleared.")
    End Sub
End Class
