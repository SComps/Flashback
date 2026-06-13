Imports System

Namespace Models
    Public Class PrintJob
        Public Property JobId As Guid
        Public Property ReceivedTime As DateTime
        Public Property SpoolFilePath As String
        Public Property State As JobState
        Public Property RetryCount As Integer
        Public Property LastAttemptTime As DateTime
        Public Property FileSize As Long
        Public Property SourceEndpoint As String

        Public Sub New()
            JobId = Guid.NewGuid()
            ReceivedTime = DateTime.Now
            State = JobState.Receiving
            RetryCount = 0
            LastAttemptTime = DateTime.MinValue
            FileSize = 0
            SourceEndpoint = String.Empty
            SpoolFilePath = String.Empty
        End Sub

        Public Overrides Function ToString() As String
            Return $"Job {JobId:N} [{State}] from {SourceEndpoint} ({FileSize} bytes)"
        End Function
    End Class

    Public Enum JobState
        Receiving = 0
        Spooled = 1
        Queued = 2
        Transmitting = 3
        Completed = 4
        Failed = 5
        Expired = 6
    End Enum
End Namespace
