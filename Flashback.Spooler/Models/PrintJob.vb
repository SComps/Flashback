Imports System

Namespace Models
    ''' <summary>
    ''' Represents a print job received on port 9100 and queued for transmission to Flashback.Engine
    ''' </summary>
    Public Class PrintJob
        ''' <summary>
        ''' Unique identifier for this job
        ''' </summary>
        Public Property JobId As Guid

        ''' <summary>
        ''' Timestamp when the job was received
        ''' </summary>
        Public Property ReceivedTime As DateTime

        ''' <summary>
        ''' Path to the temporary spool file containing the job data
        ''' </summary>
        Public Property SpoolFilePath As String

        ''' <summary>
        ''' Current state of the job
        ''' </summary>
        Public Property State As JobState

        ''' <summary>
        ''' Number of transmission retry attempts
        ''' </summary>
        Public Property RetryCount As Integer

        ''' <summary>
        ''' Timestamp of the last transmission attempt
        ''' </summary>
        Public Property LastAttemptTime As DateTime

        ''' <summary>
        ''' Size of the job data in bytes
        ''' </summary>
        Public Property FileSize As Long

        ''' <summary>
        ''' Source endpoint (IP:Port) that sent this job
        ''' </summary>
        Public Property SourceEndpoint As String

        ''' <summary>
        ''' Creates a new print job with default values
        ''' </summary>
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

        ''' <summary>
        ''' Returns a string representation of this job
        ''' </summary>
        Public Overrides Function ToString() As String
            Return $"Job {JobId:N} [{State}] from {SourceEndpoint} ({FileSize} bytes)"
        End Function
    End Class

    ''' <summary>
    ''' Represents the current state of a print job
    ''' </summary>
    Public Enum JobState
        ''' <summary>
        ''' Job is currently being received on port 9100
        ''' </summary>
        Receiving = 0

        ''' <summary>
        ''' Job has been completely received and stored in spool file
        ''' </summary>
        Spooled = 1

        ''' <summary>
        ''' Job is queued and waiting for Engine connection
        ''' </summary>
        Queued = 2

        ''' <summary>
        ''' Job is currently being transmitted to Engine
        ''' </summary>
        Transmitting = 3

        ''' <summary>
        ''' Job was successfully transmitted to Engine
        ''' </summary>
        Completed = 4

        ''' <summary>
        ''' Job transmission failed (will retry if configured)
        ''' </summary>
        Failed = 5

        ''' <summary>
        ''' Job exceeded retry limit or age limit
        ''' </summary>
        Expired = 6
    End Enum
End Namespace

' Made with Bob
